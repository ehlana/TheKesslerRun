using System.Text;
using FluentAssertions;

namespace OrbPak.Test;
public class OrbPakFormatTests
{
    [Theory]
    [InlineData(OrbPakHashType.CRC32)]
    [InlineData(OrbPakHashType.SHA1)]
    [InlineData(OrbPakHashType.SHA256)]
    public void Roundtrip_WithDifferentHashes(OrbPakHashType hashType)
    {
        var builder = new OrbPakBuilder(
            options: OrbPakOptions.None,
            hashType: hashType);

        builder.AddFile("foo.txt", Encoding.UTF8.GetBytes("content"));

        using var ms = new MemoryStream();
        builder.Save(ms);

        ms.Position = 0;
        using var pak = new OrbPakArchive(ms);

        pak.HashType.Should().Be(hashType);
        var outBytes = pak.Read("foo.txt");
        Encoding.UTF8.GetString(outBytes).Should().Be("content");
    }

    [Fact]
    public void VerifyManifest_Throws_WhenNoManifestPresent()
    {
        var builder = new OrbPakBuilder(
            options: OrbPakOptions.Compressed, // no manifest flag
            hashType: OrbPakHashType.SHA256);

        builder.AddFile("x.bin", new byte[] { 1, 2, 3 });

        using var ms = new MemoryStream();
        builder.Save(ms);

        ms.Position = 0;
        using var pak = new OrbPakArchive(ms);

        pak.Invoking(p => p.VerifyManifest())
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void InvalidMagic_ShouldThrowOnOpen()
    {
        // Create a bogus file with invalid magic
        byte[] bad = new byte[24]; // header size
        // "BORK" instead of "ORBP"
        Encoding.ASCII.GetBytes("BORK").CopyTo(bad, 0);

        using var ms = new MemoryStream(bad);
        Action act = () => new OrbPakArchive(ms);
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*Invalid ORBPAK magic*");
    }
}
