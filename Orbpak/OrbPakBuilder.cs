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
public sealed class OrbPakBuilder(OrbPakOptions options, OrbPakHashType hashType)
{
    private readonly List<OrbPakBuilder.Entry> _entries = [];

    public void AddFile(string virtualPath, byte[] data)
    {
        if (string.IsNullOrWhiteSpace(virtualPath))
            throw new ArgumentException("virtualPath is required.");
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        // ISSUE: reference to a compiler-generated field
        OrbPakBuilder.Entry entry = new OrbPakBuilder.Entry()
        {
            Filename = OrbPakBuilder.NormalizePath(virtualPath),
            Uncompressed = data,
            Length = (uint)data.Length,
            Stored = options.HasFlag((Enum)OrbPakOptions.Compressed) ? OrbPakBuilder.Deflate(data) : data
        };
        entry.StoredLength = (uint)entry.Stored.Length;
        // ISSUE: reference to a compiler-generated field
        entry.Hash = OrbPakBuilder.ComputeHash(entry.Stored, hashType);
        this._entries.Add(entry);
    }

    public void AddFile(string virtualPath, Stream input)
    {
        using (MemoryStream destination = new MemoryStream())
        {
            input.CopyTo((Stream)destination);
            this.AddFile(virtualPath, destination.ToArray());
        }
    }

    public void Save(Stream output)
    {
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        OrbPakHeader orbPakHeader = new OrbPakHeader()
        {
            SpecVersion = 3,
            FileCount = (ushort)this._entries.Count,
            IndexOffset = 24,
            OptionsFlags = (uint)options,
            HashType = (byte)hashType,
            Reserved0 = 0,
            Reserved1 = 0,
            Reserved2 = 0,
            GlobalHashOffset = 0
        };
        // ISSUE: reference to a compiler-generated field
        OrbPakHashType hashTypeP = hashType;
        if (true)
            ;
        int num1;
        switch (hashTypeP)
        {
            case OrbPakHashType.None:
                num1 = 0;
                break;
            case OrbPakHashType.CRC32:
                num1 = 4;
                break;
            case OrbPakHashType.SHA1:
                num1 = 20;
                break;
            case OrbPakHashType.SHA256:
                num1 = 32;
                break;
            default:
                throw new NotSupportedException();
        }
        if (true)
            ;
        int num2 = num1;
        long num3 = (long)(24 + (OrbPakIndexEntry.FixedSizeWithoutHash + num2) * this._entries.Count);
        foreach (OrbPakBuilder.Entry entry in this._entries)
        {
            entry.Offset = checked((uint)num3);
            num3 += (long)entry.Stored.Length;
        }
        long num4 = 0;
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        if (options.HasFlag((Enum)OrbPakOptions.ManifestHash) && hashType != 0)
      {
            num4 = num3;
            orbPakHeader.GlobalHashOffset = checked((uint)num4);
            long num5 = num3 + (long)num2;
        }
        // ISSUE: untyped stack allocation
        Span<byte> span = stackalloc byte[24];
        orbPakHeader.Write(span);
        output.Write((ReadOnlySpan<byte>)span);
        using (BinaryWriter bw = new BinaryWriter(output, Encoding.UTF8, true))
        {
            foreach (OrbPakBuilder.Entry entry in this._entries)
            {
                // ISSUE: reference to a compiler-generated field
                new OrbPakIndexEntry()
                {
                    Filename = entry.Filename,
                    Offset = entry.Offset,
                    Length = entry.Length,
                    StoredLength = entry.StoredLength,
                    Hash = entry.Hash
                }.Write(bw, hashType);
            }
        }
        foreach (OrbPakBuilder.Entry entry in this._entries)
            output.Write(entry.Stored, 0, entry.Stored.Length);
        if (num4 <= 0L)
            return;
        if (!output.CanSeek)
            throw new InvalidOperationException("Output stream must be seekable to write manifest.");
        long indexOffset = (long)orbPakHeader.IndexOffset;
        long count = num4 - indexOffset;
        output.Flush();
        long position = output.Position;
        output.Position = indexOffset;
        // ISSUE: reference to a compiler-generated field
        using (HashAlgorithm hashAlgorithm = OrbPakBuilder.CreateHashAlgorithm(hashType))
        {
            using (CryptoStream dst = new CryptoStream(Stream.Null, (ICryptoTransform)hashAlgorithm, CryptoStreamMode.Write))
            {
                OrbPakBuilder.CopyRange(output, (Stream)dst, count);
                dst.FlushFinalBlock();
            }
            byte[] hash = hashAlgorithm.Hash;
            output.Position = num4;
            output.Write(hash, 0, hash.Length);
            output.Position = position;
        }
    }

    public void Save(string filePath)
    {
        using (FileStream output = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            this.Save((Stream)output);
    }

    private static string NormalizePath(string p) => p.Replace('\\', '/');

    private static byte[] Deflate(byte[] data)
    {
        using (MemoryStream memoryStream = new MemoryStream())
        {
            using (DeflateStream deflateStream = new DeflateStream((Stream)memoryStream, CompressionLevel.SmallestSize, true))
                deflateStream.Write(data, 0, data.Length);
            return memoryStream.ToArray();
        }
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

    private static byte[] ComputeHash(byte[] data, OrbPakHashType t)
    {
        if (true)
            ;
        byte[] hash;
        switch (t)
        {
            case OrbPakHashType.None:
                hash = Array.Empty<byte>();
                break;
            case OrbPakHashType.CRC32:
                hash = BitConverter.GetBytes(Crc32.HashToUInt32((ReadOnlySpan<byte>)data));
                break;
            case OrbPakHashType.SHA1:
                hash = SHA1.HashData(data);
                break;
            case OrbPakHashType.SHA256:
                hash = SHA256.HashData(data);
                break;
            default:
                throw new NotSupportedException();
        }
        if (true)
            ;
        return hash;
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

    private sealed class Entry
    {
        public string Filename = "";
        public byte[] Uncompressed = Array.Empty<byte>();
        public byte[] Stored = Array.Empty<byte>();
        public uint Length;
        public uint StoredLength;
        public byte[] Hash = Array.Empty<byte>();
        public uint Offset;
    }
}