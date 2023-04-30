using static System.Math;
using static System.IO.File;

namespace Protonox.Web
{
    internal static class UserAgents
    {
        public static string Get(int hash)
        {
            string[] lines = ReadAllLines("./UserAgents");
            return lines[Abs(hash % lines.Length)];
        }
    }
}
