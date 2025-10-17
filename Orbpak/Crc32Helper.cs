using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.IO.Hashing;

namespace OrbPak;

internal static class Crc32Helper
{
    public static uint Compute(ReadOnlySpan<byte> data) => Crc32.HashToUInt32(data);

    public static uint Compute(Stream stream)
    {
        var crc = new Crc32();
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                crc.Append(buffer.AsSpan(0, read));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        Span<byte> hash = stackalloc byte[crc.HashLengthInBytes];
        crc.GetCurrentHash(hash);
        return BinaryPrimitives.ReadUInt32BigEndian(hash);
    }
}
