using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OneRosterSync.Net.Utils
{
    public static class Helper
    {
        public static bool ToBoolean(this string s)
        {
            string[] trueStrings = { "1", "y", "yes", "true" };
            string[] falseStrings = { "0", "n", "no", "false" };


            if (trueStrings.Contains(s, StringComparer.OrdinalIgnoreCase))
                return true;
            if (falseStrings.Contains(s, StringComparer.OrdinalIgnoreCase))
                return false;

            throw new InvalidCastException("only the following are supported for converting strings to boolean: "
                + string.Join(",", trueStrings)
                + " and "
                + string.Join(",", falseStrings));
        }
    }
}
