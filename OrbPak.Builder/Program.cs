using OrbPak;
using System.Text;

if (args.Length != 2)
{
    Console.WriteLine("Usage: OrbPak.Builder <manifest.txt> <output.orbpak>");
    return 1;
}

var manifestPath = args[0];
var outputPath = args[1];

var options = OrbPakOptions.Compressed | OrbPakOptions.ManifestHash;
var builder = new OrbPakBuilder(options, OrbPakHashType.SHA256);

foreach (var line in File.ReadAllLines(manifestPath))
{
    var trimmed = line.Trim();
    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
        continue;

    var fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath)!, trimmed));
    builder.AddFile(trimmed.Replace('\\', '/'), File.ReadAllBytes(fullPath));
}

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
using var fs = File.Create(outputPath);
builder.Save(fs);

Console.WriteLine($"[OrbPak] Packed {outputPath}.");
return 0;
