using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO.Hashing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OrbPak;
public sealed class OrbPakArchive : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly OrbPakHeader _header;
    private readonly List<OrbPakIndexEntry> _index;

    public OrbPakArchive(Stream stream, bool leaveOpen = false)
    {
        this._stream = stream ?? throw new ArgumentNullException(nameof(stream));
        this._leaveOpen = leaveOpen;
        // ISSUE: untyped stack allocation
        Span<byte> span = stackalloc byte[24];
        this._header = this._stream.Read(span) == 24 ? OrbPakHeader.Read(span) : throw new InvalidDataException("Failed to read ORBPAK header.");
        this.Options = (OrbPakOptions)this._header.OptionsFlags;
        this.HashType = (OrbPakHashType)this._header.HashType;
        this._stream.Position = (long)this._header.IndexOffset;
        this._index = new List<OrbPakIndexEntry>((int)this._header.FileCount);
        using (BinaryReader br = new BinaryReader(this._stream, Encoding.UTF8, true))
        {
            for (int index = 0; index < (int)this._header.FileCount; ++index)
                this._index.Add(OrbPakIndexEntry.Read(br, this.HashType));
        }
    }

    public static OrbPakArchive Open(string filePath)
    {
        return new OrbPakArchive((Stream)new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    public static OrbPakArchive Open(Stream stream, bool leaveOpen = false)
    {
        return new OrbPakArchive(stream, leaveOpen);
    }

    public IReadOnlyList<string> Files
    {
        get
        {
            return (IReadOnlyList<string>)this._index.Select<OrbPakIndexEntry, string>((Func<OrbPakIndexEntry, string>)(e => e.Filename)).ToList<string>();
        }
    }

    public OrbPakOptions Options { get; }

    public OrbPakHashType HashType { get; }

    public ushort SpecVersion => this._header.SpecVersion;

    public byte[] ReadStored(string virtualPath)
    {
        OrbPakIndexEntry orbPakIndexEntry = this.Find(virtualPath);
        this._stream.Position = (long)orbPakIndexEntry.Offset;
        return OrbPakArchive.ReadExact(this._stream, (int)orbPakIndexEntry.StoredLength);
    }

    public byte[] Read(string virtualPath)
    {
        OrbPakIndexEntry orbPakIndexEntry = this.Find(virtualPath);
        this._stream.Position = (long)orbPakIndexEntry.Offset;
        byte[] numArray = OrbPakArchive.ReadExact(this._stream, (int)orbPakIndexEntry.StoredLength);
        this.VerifyFileHash(numArray, orbPakIndexEntry.Hash);
        if (this.Options.HasFlag((Enum)OrbPakOptions.Compressed))
        {
            using (MemoryStream memoryStream = new MemoryStream(numArray))
            {
                using (DeflateStream deflateStream = new DeflateStream((Stream)memoryStream, CompressionMode.Decompress))
                {
                    using (MemoryStream destination = new MemoryStream((int)orbPakIndexEntry.Length))
                    {
                        deflateStream.CopyTo((Stream)destination);
                        byte[] array = destination.ToArray();
                        if (array.Length != (int)orbPakIndexEntry.Length)
                            throw new InvalidDataException("Length mismatch after decompression for " + orbPakIndexEntry.Filename + ".");
                        return array;
                    }
                }
            }
        }
        else
        {
            if (numArray.Length != (int)orbPakIndexEntry.Length)
                throw new InvalidDataException("Length mismatch for " + orbPakIndexEntry.Filename + ".");
            return numArray;
        }
    }

    public void VerifyManifest()
    {
        if (!this.Options.HasFlag((Enum)OrbPakOptions.ManifestHash))
            throw new InvalidOperationException("Archive has no manifest hash.");
        if (this.HashType == OrbPakHashType.None)
            throw new InvalidOperationException("Archive uses no hash type.");
        if (!this._stream.CanSeek)
            throw new InvalidOperationException("Stream must be seekable for verification.");
        long indexOffset = (long)this._header.IndexOffset;
        long count = (long)(this._header.GlobalHashOffset - this._header.IndexOffset);
        this._stream.Position = indexOffset;
        using (HashAlgorithm hashAlgorithm = OrbPakArchive.CreateHashAlgorithm(this.HashType))
        {
            using (CryptoStream dst = new CryptoStream(Stream.Null, (ICryptoTransform)hashAlgorithm, CryptoStreamMode.Write))
            {
                OrbPakArchive.CopyRange(this._stream, (Stream)dst, count);
                dst.FlushFinalBlock();
            }
            byte[] hash = hashAlgorithm.Hash;
            this._stream.Position = (long)this._header.GlobalHashOffset;
            if (!OrbPakArchive.ReadExact(this._stream, hash.Length).AsSpan<byte>().SequenceEqual<byte>((ReadOnlySpan<byte>)hash))
                throw new CryptographicException("ORBPAK manifest hash mismatch (archive tampered or corrupted).");
        }
    }

    private OrbPakIndexEntry Find(string path)
    {
        path = path.Replace('\\', '/');
        return this._index.FirstOrDefault<OrbPakIndexEntry>((Func<OrbPakIndexEntry, bool>)(i => i.Filename.Equals(path, StringComparison.Ordinal))) ?? throw new FileNotFoundException("Entry not found: " + path);
    }

    private void VerifyFileHash(byte[] stored, byte[] expected)
    {
        if (this.HashType == OrbPakHashType.None)
            return;
        OrbPakHashType hashType = this.HashType;
        if (true)
            ;
        byte[] array;
        switch (hashType)
        {
            case OrbPakHashType.CRC32:
                array = BitConverter.GetBytes(Crc32.HashToUInt32((ReadOnlySpan<byte>)stored));
                break;
            case OrbPakHashType.SHA1:
                array = SHA1.HashData(stored);
                break;
            case OrbPakHashType.SHA256:
                array = SHA256.HashData(stored);
                break;
            default:
                throw new NotSupportedException();
        }
        if (true)
            ;
        if (!array.AsSpan<byte>().SequenceEqual<byte>((ReadOnlySpan<byte>)expected))
            throw new CryptographicException("ORBPAK entry hash mismatch (file tampered or corrupted).");
    }

    private static byte[] ReadExact(Stream s, int count)
    {
        byte[] buffer = new byte[count];
        int num;
        for (int offset = 0; offset < count; offset += num)
        {
            num = s.Read(buffer, offset, count - offset);
            if (num <= 0)
                throw new EndOfStreamException();
        }
        return buffer;
    }

    private static HashAlgorithm? CreateHashAlgorithm(OrbPakHashType t)
    {
        if (true)
            ;
        HashAlgorithm hashAlgorithm;
        switch (t)
        {
            case OrbPakHashType.SHA1:
                hashAlgorithm = (HashAlgorithm)SHA1.Create();
                break;
            case OrbPakHashType.SHA256:
                hashAlgorithm = (HashAlgorithm)SHA256.Create();
                break;
            default:
                hashAlgorithm = (HashAlgorithm)null;
                break;
        }
        if (true)
            ;
        return hashAlgorithm;
    }

    private static void CopyRange(Stream src, Stream dst, long count)
    {
        byte[] numArray = ArrayPool<byte>.Shared.Rent(65536);
        try
        {
            int count1;
            for (; count > 0L; count -= (long)count1)
            {
                int count2 = (int)Math.Min((long)numArray.Length, count);
                count1 = src.Read(numArray, 0, count2);
                if (count1 <= 0)
                    throw new EndOfStreamException();
                dst.Write(numArray, 0, count1);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(numArray);
        }
    }

    public void Dispose()
    {
        if (this._leaveOpen)
            return;
        this._stream.Dispose();
    }
}
