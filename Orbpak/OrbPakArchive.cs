using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace OrbPak;

public sealed class OrbPakArchive : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly OrbPakHeader _header;
    private readonly List<OrbPakIndexEntry> _index;

    public OrbPakArchive(Stream stream, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;

        Span<byte> headerBuffer = stackalloc byte[OrbPakSpec.HeaderSize];
        _stream.ReadExactly(headerBuffer);
        _header = OrbPakHeader.Read(headerBuffer);

        Options = (OrbPakOptions)_header.OptionsFlags;
        HashType = (OrbPakHashType)_header.HashType;

        _stream.Position = _header.IndexOffset;
        _index = new List<OrbPakIndexEntry>(_header.FileCount);

        using var reader = new BinaryReader(_stream, Encoding.UTF8, leaveOpen: true);
        for (int i = 0; i < _header.FileCount; i++)
        {
            _index.Add(OrbPakIndexEntry.Read(reader, HashType));
        }
    }

    public static OrbPakArchive Open(string filePath)
    {
        var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new OrbPakArchive(file);
    }

    public IReadOnlyList<string> Files => _index.Select(e => e.Filename).ToList();

    public OrbPakOptions Options { get; }

    public OrbPakHashType HashType { get; }

    public ushort SpecVersion => _header.SpecVersion;

    public byte[] ReadStored(string virtualPath)
    {
        var entry = Find(virtualPath);
        _stream.Position = entry.Offset;
        return ReadExact(_stream, (int)entry.StoredLength);
    }

    public byte[] Read(string virtualPath)
    {
        var entry = Find(virtualPath);
        _stream.Position = entry.Offset;

        var stored = ReadExact(_stream, (int)entry.StoredLength);
        VerifyFileHash(stored, entry.Hash);

        if (!Options.HasFlag(OrbPakOptions.Compressed))
        {
            if (stored.Length != entry.Length)
            {
                throw new InvalidDataException($"Length mismatch for {entry.Filename}.");
            }

            return stored;
        }

        using var compressed = new MemoryStream(stored, writable: false);
        using var inflater = new DeflateStream(compressed, CompressionMode.Decompress, leaveOpen: true);
        using var destination = new MemoryStream((int)entry.Length);
        inflater.CopyTo(destination);

        var result = destination.ToArray();
        if (result.Length != entry.Length)
        {
            throw new InvalidDataException($"Length mismatch after decompression for {entry.Filename}.");
        }

        return result;
    }

    public void VerifyManifest()
    {
        if (!Options.HasFlag(OrbPakOptions.ManifestHash))
            throw new InvalidOperationException("Archive has no manifest hash.");
        if (HashType == OrbPakHashType.None)
            throw new InvalidOperationException("Archive does not use a hash type.");
        if (!_stream.CanSeek)
            throw new InvalidOperationException("Stream must support seeking to verify the manifest.");

        long manifestStart = _header.IndexOffset;
        long manifestLength = _header.GlobalHashOffset - _header.IndexOffset;

        _stream.Position = manifestStart;
        using var hashAlgorithm = CreateHashAlgorithm(HashType)
            ?? throw new InvalidOperationException("Hash algorithm is not available.");
        using (var crypto = new CryptoStream(Stream.Null, hashAlgorithm, CryptoStreamMode.Write))
        {
            CopyRange(_stream, crypto, manifestLength);
            crypto.FlushFinalBlock();
        }

        byte[] computed = hashAlgorithm.Hash!;
        _stream.Position = _header.GlobalHashOffset;
        byte[] stored = ReadExact(_stream, computed.Length);

        if (!stored.AsSpan().SequenceEqual(computed))
        {
            throw new CryptographicException("ORBPAK manifest hash mismatch (archive tampered or corrupted).");
        }
    }

    private OrbPakIndexEntry Find(string path)
    {
        var normalised = path.Replace('\\', '/');
        return _index.FirstOrDefault(entry => entry.Filename.Equals(normalised, StringComparison.Ordinal))
               ?? throw new FileNotFoundException($"Entry not found: {path}");
    }

    private void VerifyFileHash(byte[] stored, byte[] expected)
    {
        if (HashType == OrbPakHashType.None)
        {
            return;
        }

        byte[] actual = HashType switch
        {
            OrbPakHashType.CRC32 => BitConverter.GetBytes(Crc32Helper.Compute(stored)),
            OrbPakHashType.SHA1 => SHA1.HashData(stored),
            OrbPakHashType.SHA256 => SHA256.HashData(stored),
            _ => throw new NotSupportedException($"Unknown hash type {HashType}.")
        };

        if (!actual.AsSpan().SequenceEqual(expected))
        {
            throw new CryptographicException("ORBPAK entry hash mismatch (file tampered or corrupted).");
        }
    }

    private static byte[] ReadExact(Stream stream, int count)
    {
        var buffer = new byte[count];
        stream.ReadExactly(buffer);
        return buffer;
    }

    private static HashAlgorithm? CreateHashAlgorithm(OrbPakHashType hashType) => hashType switch
    {
        OrbPakHashType.SHA1 => SHA1.Create(),
        OrbPakHashType.SHA256 => SHA256.Create(),
        _ => null
    };

    private static void CopyRange(Stream source, Stream destination, long count)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            while (count > 0)
            {
                int read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
                if (read <= 0)
                {
                    throw new EndOfStreamException();
                }

                destination.Write(buffer, 0, read);
                count -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void Dispose()
    {
        if (_leaveOpen)
        {
            return;
        }

        _stream.Dispose();
    }
}
