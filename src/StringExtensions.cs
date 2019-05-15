using System;

namespace UnityNativeTool.Internal
{
    internal static class StringExtensions
    {
        public static string TrimStart(this string str, params string[] trimString)
        {
            foreach(var s in trimString)
            {
                if(str.StartsWith(s))
                {
                    str = str.Substring(s.Length);
                }
            }

            return str;
        }

        public static string TrimEnd(this string str, params string[] trimString)
        {
            foreach (var s in trimString)
            {
                if (str.EndsWith(s))
                {
                    str = str.Remove(str.Length - s.Length - 1);
                }
            }

            return str;
        }
    }
}
