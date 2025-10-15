using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrbPak;
[Flags]
public enum OrbPakOptions : uint
{
    None = 0,
    Compressed = 1,
    Encrypted = 2,
    DevMetadata = 4,
    ManifestHash = 8,
    ModOverride = 16, // 0x00000010
}