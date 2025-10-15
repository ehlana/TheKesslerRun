using System.Text;

namespace OrbPak;
public static class OrbPakHelpers
{
    public static string ReadAsText(this OrbPakArchive archive, string path)
    {
        var bytes = archive.Read(path);
        return Encoding.UTF8.GetString(bytes);
    }
}
