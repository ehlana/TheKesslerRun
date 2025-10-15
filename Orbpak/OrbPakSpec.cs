using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrbPak;
public static class OrbPakSpec
{
    public const string Magic = "ORBP";
    public const ushort SpecVersion = 3;
    public const int HeaderSize = 24;
    public const int FilenameBytes = 64;
}