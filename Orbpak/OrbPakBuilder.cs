using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace OrbPak;

public sealed class OrbPakBuilder(OrbPakOptions options, OrbPakHashType hashType)
{
    private readonly List<Entry> _entries = [];

    public void AddFile(string virtualPath, byte[] data)
    {
        if (string.IsNullOrWhiteSpace(virtualPath))
            throw new ArgumentException("virtualPath is required.", nameof(virtualPath));
        if (data is null)
            throw new ArgumentNullException(nameof(data));

        var normalized = NormalizePath(virtualPath);
        var stored = options.HasFlag(OrbPakOptions.Compressed) ? Deflate(data) : data;
        var hash = ComputeHash(stored, hashType);
        var entry = new Entry(normalized, (uint)data.Length, stored, hash);
        _entries.Add(entry);
    }

    public void AddFile(string virtualPath, Stream input)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        AddFile(virtualPath, buffer.ToArray());
    }

    public void Save(Stream output)
    {
        if (output is null)
            throw new ArgumentNullException(nameof(output));
        if (!output.CanWrite)
            throw new ArgumentException("Output stream must be writable.", nameof(output));

        int hashLength = GetHashLength(hashType);
        long indexStart = OrbPakSpec.HeaderSize;
        long dataOffset = indexStart + (_entries.Count * (OrbPakIndexEntry.FixedSizeWithoutHash + hashLength));

        foreach (var entry in _entries)
        {
            entry.Offset = (uint)dataOffset;
            dataOffset += entry.Stored.Length;
        }

        uint manifestOffset = 0;
        if (options.HasFlag(OrbPakOptions.ManifestHash) && hashType != OrbPakHashType.None)
        {
            manifestOffset = (uint)dataOffset;
            dataOffset += hashLength;
        }

        var header = new OrbPakHeader
        {
            Magic = 0, // overwritten by Write
            SpecVersion = OrbPakSpec.Version,
            FileCount = (ushort)_entries.Count,
            IndexOffset = (uint)indexStart,
            OptionsFlags = (uint)options,
            HashType = (byte)hashType,
            GlobalHashOffset = manifestOffset
        };

        Span<byte> headerBuffer = stackalloc byte[OrbPakSpec.HeaderSize];
        header.Write(headerBuffer);
        output.Write(headerBuffer);

        using (var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
        {
            foreach (var entry in _entries)
            {
                entry.Write(writer, hashType);
            }
        }

        foreach (var entry in _entries)
        {
            output.Write(entry.Stored, 0, entry.Stored.Length);
        }

        if (manifestOffset == 0)
        {
            return;
        }

        if (!output.CanSeek)
            throw new InvalidOperationException("Output stream must be seekable to write the manifest hash.");

        long manifestStart = header.IndexOffset;
        long manifestLength = manifestOffset - manifestStart;

        output.Flush();
        long originalPosition = output.Position;
        output.Position = manifestStart;

        using var hashAlgorithm = CreateHashAlgorithm(hashType)
            ?? throw new InvalidOperationException("Manifest hash requested but algorithm is unavailable.");
        using (var crypto = new CryptoStream(Stream.Null, hashAlgorithm, CryptoStreamMode.Write))
        {
            CopyRange(output, crypto, manifestLength);
            crypto.FlushFinalBlock();
        }

        output.Position = manifestOffset;
        byte[] manifestHash = hashAlgorithm.Hash!;
        output.Write(manifestHash, 0, manifestHash.Length);
        output.Position = originalPosition;
    }

    public void Save(string filePath)
    {
        using var file = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        Save(file);
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var compressor = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            compressor.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }

    private static HashAlgorithm? CreateHashAlgorithm(OrbPakHashType hashType) => hashType switch
    {
        OrbPakHashType.SHA1 => SHA1.Create(),
        OrbPakHashType.SHA256 => SHA256.Create(),
        _ => null
    };

    private static byte[] ComputeHash(byte[] data, OrbPakHashType hashType) => hashType switch
    {
        OrbPakHashType.None => Array.Empty<byte>(),
        OrbPakHashType.CRC32 => BitConverter.GetBytes(Crc32Helper.Compute(data)),
        OrbPakHashType.SHA1 => SHA1.HashData(data),
        OrbPakHashType.SHA256 => SHA256.HashData(data),
        _ => throw new NotSupportedException($"Unknown hash type '{hashType}'.")
    };

    private static int GetHashLength(OrbPakHashType hashType) => hashType switch
    {
        OrbPakHashType.None => 0,
        OrbPakHashType.CRC32 => 4,
        OrbPakHashType.SHA1 => 20,
        OrbPakHashType.SHA256 => 32,
        _ => throw new NotSupportedException($"Unknown hash type '{hashType}'.")
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
                    throw new EndOfStreamException();

                destination.Write(buffer, 0, read);
                count -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private sealed record Entry(string Filename, uint Length, byte[] Stored, byte[] Hash)
    {
        public uint Offset { get; set; }

        public void Write(BinaryWriter writer, OrbPakHashType hashType)
        {
            var index = new OrbPakIndexEntry
            {
                Filename = Filename,
                Offset = Offset,
                Length = Length,
                StoredLength = (uint)Stored.Length,
                Hash = Hash
            };

            index.Write(writer, hashType);
        }
    }
}
