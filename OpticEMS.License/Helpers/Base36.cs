using System;
using System.Collections.Generic;
using System.Text;

namespace OpticEMS.License.Helpers
{
    internal static class Base36
    {
        private const string CharList = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private static readonly char[] CharArray = CharList.ToCharArray();

        public static long Decode(string input)
        {
            long result = 0;
            double pow = 0;
            for (var i = input.Length - 1; i >= 0; i--)
            {
                var c = input[i];
                var pos = CharList.IndexOf(c);
                if (pos > -1)
                    result += pos * (long)Math.Pow(CharList.Length, pow);
                else
                    return -1;
                pow++;
            }
            return result;
        }

        public static string Encode(ulong input)
        {
            var sb = new StringBuilder();
            do
            {
                sb.Append(CharArray[input % (ulong)CharList.Length]);
                input /= (ulong)CharList.Length;
            } while (input != 0);

            return Reverse(sb.ToString());
        }

        private static string Reverse(string s)
        {
            var charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }
    }
}
