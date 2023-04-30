using System;
using static System.Math;
using static System.IO.File;

namespace Protonox.Web
{
    internal static class Proxys
    {
        public static Uri Get(int hash)
        {
            string[] lines = ReadAllLines("./Proxys");
            return new Uri(lines[Abs(hash % lines.Length)]);
        }
    }
}
