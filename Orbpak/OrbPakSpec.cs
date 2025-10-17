namespace OrbPak;

/// <summary>
/// Shared constants for the ORBPAK v3 file format.
/// </summary>
public static class OrbPakSpec
{
    public const string Magic = "ORBP";
    public const ushort Version = 3;
    public const int HeaderSize = 24;
    public const int FilenameBytes = 64; // UTF-8, null terminated
}
