using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OrbPak;
internal sealed class OrbPakIndexEntry
{
    public string Filename = "";
    public uint Offset;
    public uint Length;
    public uint StoredLength;
    public byte[] Hash = Array.Empty<byte>();

    public static int FixedSizeWithoutHash => 76;

    public void Write(BinaryWriter bw, OrbPakHashType hashType)
    {
        OrbPakIndexEntry.WriteFixedName(bw, this.Filename);
        bw.Write(this.Offset);
        bw.Write(this.Length);
        bw.Write(this.StoredLength);
        switch (hashType)
        {
            case OrbPakHashType.None:
                break;
            case OrbPakHashType.CRC32:
                EnsureHashLen(4);
                bw.Write(this.Hash);
                break;
            case OrbPakHashType.SHA1:
                EnsureHashLen(20);
                bw.Write(this.Hash);
                break;
            case OrbPakHashType.SHA256:
                EnsureHashLen(32);
                bw.Write(this.Hash);
                break;
            default:
                throw new NotSupportedException("Unknown hash type");
        }

        void EnsureHashLen(int n)
        {
            if (this.Hash.Length != n)
            {
                DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(44, 3);
                interpolatedStringHandler.AppendLiteral("Hash length mismatch for ");
                interpolatedStringHandler.AppendFormatted(this.Filename);
                interpolatedStringHandler.AppendLiteral(" (expected ");
                interpolatedStringHandler.AppendFormatted<int>(n);
                interpolatedStringHandler.AppendLiteral(", got ");
                interpolatedStringHandler.AppendFormatted<int>(this.Hash.Length);
                interpolatedStringHandler.AppendLiteral(").");
                throw new InvalidDataException(interpolatedStringHandler.ToStringAndClear());
            }
        }
    }

    public static OrbPakIndexEntry Read(BinaryReader br, OrbPakHashType hashType)
    {
        OrbPakIndexEntry orbPakIndexEntry1 = new OrbPakIndexEntry();
        orbPakIndexEntry1.Filename = OrbPakIndexEntry.ReadFixedName(br);
        orbPakIndexEntry1.Offset = br.ReadUInt32();
        orbPakIndexEntry1.Length = br.ReadUInt32();
        orbPakIndexEntry1.StoredLength = br.ReadUInt32();
        OrbPakIndexEntry orbPakIndexEntry2 = orbPakIndexEntry1;
        if (true)
            ;
        byte[] numArray;
        switch (hashType)
        {
            case OrbPakHashType.None:
                numArray = Array.Empty<byte>();
                break;
            case OrbPakHashType.CRC32:
                numArray = br.ReadBytes(4);
                break;
            case OrbPakHashType.SHA1:
                numArray = br.ReadBytes(20);
                break;
            case OrbPakHashType.SHA256:
                numArray = br.ReadBytes(32);
                break;
            default:
                throw new NotSupportedException("Unknown hash type");
        }
        if (true)
            ;
        orbPakIndexEntry2.Hash = numArray;
        return orbPakIndexEntry1;
    }

    private static void WriteFixedName(BinaryWriter bw, string name)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(name);
        // ISSUE: untyped stack allocation
        Span<byte> span = stackalloc byte[64];
        span.Clear();
        int num = Math.Min(63, bytes.Length);
        bytes.AsSpan<byte>(0, num).CopyTo(span);
        span[num] = (byte)0;
        bw.Write(span);
    }

    private static string ReadFixedName(BinaryReader br)
    {
        byte[] numArray = br.ReadBytes(64);
        int count = Array.IndexOf<byte>(numArray, (byte)0);
        if (count < 0)
            count = numArray.Length;
        return Encoding.UTF8.GetString(numArray, 0, count);
    }
}