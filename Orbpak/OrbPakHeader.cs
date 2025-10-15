using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OrbPak;
internal struct OrbPakHeader
  {
    public uint Magic;
    public ushort SpecVersion;
    public ushort FileCount;
    public uint IndexOffset;
    public uint OptionsFlags;
    public byte HashType;
    public byte Reserved0;
    public byte Reserved1;
    public byte Reserved2;
    public uint GlobalHashOffset;

    public void Write(Span<byte> dst)
    {
      if (dst.Length < 24)
        throw new ArgumentException("Header buffer too small.");
      Encoding.ASCII.GetBytes("ORBP").CopyTo<byte>(dst.Slice(0, 4));
      OrbPakHeader.WriteUInt16(dst, 4, this.SpecVersion);
      OrbPakHeader.WriteUInt16(dst, 6, this.FileCount);
      OrbPakHeader.WriteUInt32(dst, 8, this.IndexOffset);
      OrbPakHeader.WriteUInt32(dst, 12, this.OptionsFlags);
      dst[16] = this.HashType;
      dst[17] = this.Reserved0;
      dst[18] = this.Reserved1;
      dst[19] = this.Reserved2;
      OrbPakHeader.WriteUInt32(dst, 20, this.GlobalHashOffset);
    }

    public static OrbPakHeader Read(ReadOnlySpan<byte> src)
    {
      if (src.Length < 24)
        throw new InvalidDataException("ORBPAK header too small.");
      OrbPakHeader orbPakHeader = src.Slice(0, 4).SequenceEqual<byte>((ReadOnlySpan<byte>) Encoding.ASCII.GetBytes("ORBP")) ? new OrbPakHeader()
      {
        Magic = BitConverter.ToUInt32(src.Slice(0, 4)),
        SpecVersion = OrbPakHeader.ReadUInt16(src, 4),
        FileCount = OrbPakHeader.ReadUInt16(src, 6),
        IndexOffset = OrbPakHeader.ReadUInt32(src, 8),
        OptionsFlags = OrbPakHeader.ReadUInt32(src, 12),
        HashType = src[16],
        Reserved0 = src[17],
        Reserved1 = src[18],
        Reserved2 = src[19],
        GlobalHashOffset = OrbPakHeader.ReadUInt32(src, 20)
      } : throw new InvalidDataException("Invalid ORBPAK magic.");
      if (orbPakHeader.SpecVersion < (ushort) 3)
      {
        DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(37, 1);
        interpolatedStringHandler.AppendLiteral("Unsupported ORBPAK version ");
        interpolatedStringHandler.AppendFormatted<ushort>(orbPakHeader.SpecVersion);
        interpolatedStringHandler.AppendLiteral(" (need 3).");
        throw new InvalidDataException(interpolatedStringHandler.ToStringAndClear());
      }
      return orbPakHeader;
    }

    private static void WriteUInt16(Span<byte> dst, int offset, ushort v)
    {
      BitConverter.TryWriteBytes(dst.Slice(offset, 2), v);
    }

    private static void WriteUInt32(Span<byte> dst, int offset, uint v)
    {
      BitConverter.TryWriteBytes(dst.Slice(offset, 4), v);
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> src, int offset)
    {
      return BitConverter.ToUInt16(src.Slice(offset, 2));
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> src, int offset)
    {
      return BitConverter.ToUInt32(src.Slice(offset, 4));
    }
  }