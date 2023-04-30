namespace Protonox
{
    public static class StringExtension
    {
        public static int DetHashGen(this string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text)
                {
                    hash = hash * 31 + c;
                }
                return hash;
            }
        }
    }
}
