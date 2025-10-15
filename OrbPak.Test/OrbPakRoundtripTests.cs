using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrbPak.Test;
public class OrbPakRoundtripTests
{
    [Fact]
    public void Roundtrip_NoCompression_NoHash_NoManifest()
    {
        var dataA = Encoding.UTF8.GetBytes("{\"hello\":\"world\"}");
        var dataB = Enumerable.Range(0, 1024).Select(i => (byte)(i % 256)).ToArray();

        var builder = new OrbPakBuilder(
            options: OrbPakOptions.None,
            hashType: OrbPakHashType.None);

        builder.AddFile("resources.json", dataA);
        builder.AddFile("bin/blob.bin", dataB);

        using var ms = new MemoryStream();
        builder.Save(ms);

        ms.Position = 0;
        using var pak = new OrbPakArchive(ms, leaveOpen: false);

        pak.SpecVersion.Should().Be(3);
        pak.HashType.Should().Be(OrbPakHashType.None);
        pak.Options.Should().Be(OrbPakOptions.None);

        pak.Files.Should().Contain(new[] { "resources.json", "bin/blob.bin" });

        var outA = pak.Read("resources.json");
        var outB = pak.Read("bin/blob.bin");

        outA.Should().Equal(dataA);
        outB.Should().Equal(dataB);

        // No manifest present
        Action act = () => pak.VerifyManifest();
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*no manifest*");
    }

    [Fact]
    public void Roundtrip_Compressed_SHA256_Manifest_Verifies()
    {
        var random = new Random(1337);
        var data = new byte[256 * 1024];
        random.NextBytes(data);

        var builder = new OrbPakBuilder(
            options: OrbPakOptions.Compressed | OrbPakOptions.ManifestHash,
            hashType: OrbPakHashType.SHA256);

        builder.AddFile("big/random.bin", data);
        builder.AddFile("cfg/fields.json", Encoding.UTF8.GetBytes("{\"x\":1}"));

        using var ms = new MemoryStream();
        builder.Save(ms);

        ms.Position = 0;
        using var pak = new OrbPakArchive(ms);

        pak.Options.HasFlag(OrbPakOptions.Compressed).Should().BeTrue();
        pak.HashType.Should().Be(OrbPakHashType.SHA256);

        // Manifest must verify
        pak.Invoking(p => p.VerifyManifest()).Should().NotThrow();

        // Read and ensure decompression worked
        var roundtrip = pak.Read("big/random.bin");
        roundtrip.Should().Equal(data);
    }
}
