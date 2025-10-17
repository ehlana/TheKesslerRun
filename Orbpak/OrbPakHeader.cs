using System.Buffers.Binary;
using System.Text;

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

    public void Write(Span<byte> buffer)
    {
        if (buffer.Length < OrbPakSpec.HeaderSize)
            throw new ArgumentException("Header buffer too small.", nameof(buffer));

        var magic = Encoding.ASCII.GetBytes(OrbPakSpec.Magic);
        magic.CopyTo(buffer[..magic.Length]);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[4..6], SpecVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[6..8], FileCount);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[8..12], IndexOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[12..16], OptionsFlags);
        buffer[16] = HashType;
        buffer[17] = Reserved0;
        buffer[18] = Reserved1;
        buffer[19] = Reserved2;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[20..24], GlobalHashOffset);
    }

    public static OrbPakHeader Read(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < OrbPakSpec.HeaderSize)
            throw new InvalidDataException("ORBPAK header truncated.");

        var magic = Encoding.ASCII.GetBytes(OrbPakSpec.Magic);
        if (!buffer[..magic.Length].SequenceEqual(magic))
            throw new InvalidDataException("Invalid ORBPAK magic.");

        var header = new OrbPakHeader
        {
            Magic = BinaryPrimitives.ReadUInt32LittleEndian(buffer[..4]),
            SpecVersion = BinaryPrimitives.ReadUInt16LittleEndian(buffer[4..6]),
            FileCount = BinaryPrimitives.ReadUInt16LittleEndian(buffer[6..8]),
            IndexOffset = BinaryPrimitives.ReadUInt32LittleEndian(buffer[8..12]),
            OptionsFlags = BinaryPrimitives.ReadUInt32LittleEndian(buffer[12..16]),
            HashType = buffer[16],
            Reserved0 = buffer[17],
            Reserved1 = buffer[18],
            Reserved2 = buffer[19],
            GlobalHashOffset = BinaryPrimitives.ReadUInt32LittleEndian(buffer[20..24])
        };

        if (header.SpecVersion < OrbPakSpec.Version)
        {
            throw new InvalidDataException(
                $"Unsupported ORBPAK version {header.SpecVersion} (expected >= {OrbPakSpec.Version}).");
        }

        return header;
    }
}
