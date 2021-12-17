using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace ASCOM.VantagePro
{
    public static class Extensions
    {
        private static readonly CultureInfo en_US = CultureInfo.CreateSpecificCulture("en-US");

        public static Double TryParseDouble_LocalThenEnUS(this double d, string str)
        {
            if (Double.TryParse(str, out double value))
                return value;

            if (Double.TryParse(str, NumberStyles.Float, en_US, out value))
                return value;

            return Double.NaN;
        }

        public static DateTime TryParseDateTime_LocalThenEnUS(this string str)
        {
            if (DateTime.TryParse(str, out DateTime d))
                return d;

            if (DateTime.TryParse(str, en_US, DateTimeStyles.None, out d))
                return d;

            return DateTime.MinValue;
        }
    }
}