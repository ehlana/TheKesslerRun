namespace OrbPak;

[Flags]
public enum OrbPakOptions : uint
{
    None = 0,
    Compressed = 1 << 0,
    Encrypted = 1 << 1,      // reserved
    DevMetadata = 1 << 2,    // reserved
    ManifestHash = 1 << 3,   // include global manifest hash at end of stream
    ModOverride = 1 << 4,    // reserved for loader policy
}
