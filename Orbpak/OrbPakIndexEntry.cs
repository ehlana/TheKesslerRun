using System.Text;

namespace OrbPak;

internal sealed class OrbPakIndexEntry
{
    public string Filename { get; init; } = string.Empty;
    public uint Offset { get; set; }
    public uint Length { get; init; }
    public uint StoredLength { get; init; }
    public byte[] Hash { get; set; } = Array.Empty<byte>();

    public static int FixedSizeWithoutHash => OrbPakSpec.FilenameBytes + sizeof(uint) * 3;

    public void Write(BinaryWriter writer, OrbPakHashType hashType)
    {
        WriteFixedName(writer, Filename);
        writer.Write(Offset);
        writer.Write(Length);
        writer.Write(StoredLength);

        int expectedHashLength = GetHashLength(hashType);
        if (expectedHashLength == 0)
        {
            return;
        }

        if (Hash.Length != expectedHashLength)
        {
            throw new InvalidDataException(
                $"Hash length mismatch for {Filename} (expected {expectedHashLength}, got {Hash.Length}).");
        }

        writer.Write(Hash);
    }

    public static OrbPakIndexEntry Read(BinaryReader reader, OrbPakHashType hashType)
    {
        var entry = new OrbPakIndexEntry
        {
            Filename = ReadFixedName(reader),
            Offset = reader.ReadUInt32(),
            Length = reader.ReadUInt32(),
            StoredLength = reader.ReadUInt32()
        };

        int hashLength = GetHashLength(hashType);
        entry.Hash = hashLength > 0 ? reader.ReadBytes(hashLength) : Array.Empty<byte>();
        return entry;
    }

    private static void WriteFixedName(BinaryWriter writer, string name)
    {
        var bytes = Encoding.UTF8.GetBytes(name ?? string.Empty);
        Span<byte> buffer = stackalloc byte[OrbPakSpec.FilenameBytes];
        buffer.Clear();

        int copyLength = Math.Min(buffer.Length - 1, bytes.Length);
        bytes.AsSpan(0, copyLength).CopyTo(buffer);
        buffer[copyLength] = 0;

        writer.Write(buffer);
    }

    private static string ReadFixedName(BinaryReader reader)
    {
        var buffer = reader.ReadBytes(OrbPakSpec.FilenameBytes);
        int terminator = Array.IndexOf(buffer, (byte)0);
        if (terminator < 0)
        {
            terminator = buffer.Length;
        }

        return Encoding.UTF8.GetString(buffer, 0, terminator);
    }

    private static int GetHashLength(OrbPakHashType hashType) => hashType switch
    {
        OrbPakHashType.None => 0,
        OrbPakHashType.CRC32 => 4,
        OrbPakHashType.SHA1 => 20,
        OrbPakHashType.SHA256 => 32,
        _ => throw new NotSupportedException($"Unknown hash type '{hashType}'.")
    };
}
