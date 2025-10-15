using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrbPak;
public enum OrbPakHashType : byte
{
    None,
    CRC32,
    SHA1,
    SHA256,
}