using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrbPak.Test;
public class OrbPakTamperTests
{
    [Fact]
    public void Tamper_PerFileHash_ShouldThrowOnRead()
    {
        // Build compressed + SHA256 (per-file hashes over stored bytes)
        var data = Enumerable.Range(0, 4096).Select(i => (byte)(i * 31)).ToArray();

        var builder = new OrbPakBuilder(
            options: OrbPakOptions.Compressed,
            hashType: OrbPakHashType.SHA256);
        builder.AddFile("data.bin", data);

        using var ms = new MemoryStream();
        builder.Save(ms);
        var bytes = ms.ToArray();

        // Corrupt first byte in the index+data region after the header.
        // Header is 24 bytes; index starts immediately after.
        // Flip a byte somewhere AFTER the index so we hit the stored data of the only file.
        // We don't know precise offset, but we can brute-force search for the first non-zero run
        // after some bytes, or simply flip somewhere near the end.
        // Safer approach: flip the last byte in the archive (almost certainly in data block).
        bytes[^1] ^= 0xFF;

        using var tampered = new MemoryStream(bytes);
        using var pak = new OrbPakArchive(tampered);

        // Attempt to read the tampered file => per-file hash mismatch
        Action act = () => pak.Read("data.bin");
        act.Should().Throw<System.Security.Cryptography.CryptographicException>()
            .WithMessage("*entry hash mismatch*");
    }

    [Fact]
    public void Tamper_ManifestHash_ShouldThrowOnVerify()
    {
        var builder = new OrbPakBuilder(
            options: OrbPakOptions.Compressed | OrbPakOptions.ManifestHash,
            hashType: OrbPakHashType.SHA256);

        builder.AddFile("a.txt", Encoding.UTF8.GetBytes("AAAA"));
        builder.AddFile("b.txt", Encoding.UTF8.GetBytes("BBBB"));

        using var ms = new MemoryStream();
        builder.Save(ms);

        var bytes = ms.ToArray();

        // Flip a byte inside the INDEX region: header = 24 bytes, index follows.
        // We'll flip byte at position 24 (the very first byte of index) to ensure manifest fails.
        bytes[24] ^= 0x01;

        using var tampered = new MemoryStream(bytes);
        using var pak = new OrbPakArchive(tampered);

        // Manifest verification must fail
        pak.Invoking(p => p.VerifyManifest())
           .Should().Throw<System.Security.Cryptography.CryptographicException>()
           .WithMessage("*manifest hash mismatch*");

        // But reading an entry might still succeed (per-file hashes may still match),
        // since we only changed the index byte (depends on what we flipped).
        // We won't assert read outcome; the point is manifest caught the tamper.
    }
}
