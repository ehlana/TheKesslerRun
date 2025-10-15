#r "OrbPak.dll"
using OrbPak;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

// --- Argument parsing ---
var argsDict = Args
    .Select(a => a.Split('=', 2))
    .ToDictionary(
        a => a[0].Trim().ToLowerInvariant(),
        a => a.Length > 1 ? a[1].Trim() : ""
    );

// Defaults
string inputFile = argsDict.TryGetValue("inputfile", out var inf) && !string.IsNullOrWhiteSpace(inf)
    ? inf
    : "data.orbpak";

string extractFile = argsDict.TryGetValue("file", out var f) ? f : null;
string outputFile = argsDict.TryGetValue("outputfile", out var of) ? of : null;

if (!File.Exists(inputFile))
{
    Console.WriteLine($"❌  {inputFile} not found");
    return;
}

using (var fs = File.OpenRead(inputFile))
using (var pak = new OrbPakArchive(fs))
{
    Console.WriteLine($"✅ Opened {inputFile}");
    Console.WriteLine($"    Spec Version: {pak.SpecVersion}");
    Console.WriteLine($"    Hash Type:    {pak.HashType}");
    Console.WriteLine($"    Options:      {pak.Options}");
    Console.WriteLine($"    Files:        {pak.Files.Count}");
    Console.WriteLine();

    if (extractFile == null)
    {
        // --- List files ---
        var idxField = typeof(OrbPakArchive)
            .GetField("_index", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var indexList = (IEnumerable<object>)idxField.GetValue(pak)!;
        int i = 1;

        foreach (var entry in indexList)
        {
            var t = entry.GetType();
            var name = (string)t.GetField("Filename")!.GetValue(entry)!;
            var offset = (uint)t.GetField("Offset")!.GetValue(entry)!;
            var length = (uint)t.GetField("Length")!.GetValue(entry)!;
            var stored = (uint)t.GetField("StoredLength")!.GetValue(entry)!;

            Console.WriteLine($"{i,3}. {name,-40}  len={length,8}  stored={stored,8}  offset={offset,10}");
            i++;
        }

        Console.WriteLine();
        Console.WriteLine("Archive listing complete.");
    }
    else
    {
        // --- Extract single file ---
        try
        {
            Console.WriteLine($"Extracting '{extractFile}'...");
            var data = pak.Read(extractFile);
            if (!string.IsNullOrEmpty(outputFile))
            {
                File.WriteAllBytes(outputFile, data);
                Console.WriteLine($"✅ Wrote {data.Length} bytes to {outputFile}");
            }
            else
            {
                Console.WriteLine($"✅ Extracted {data.Length} bytes from {extractFile}:");
                Console.WriteLine();
                var text = Encoding.UTF8.GetString(data);
                if (text.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t'))
                {
                    Console.WriteLine($"[binary data omitted]");
                }
                else
                {
                    Console.WriteLine(text.Length > 4000 ? text[..4000] + "\n[...truncated...]" : text);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to extract: {ex.Message}");
        }
    }
}
