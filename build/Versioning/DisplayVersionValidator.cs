using System;
using System.Text.RegularExpressions;

namespace Versioning
{
    public static class DisplayVersionValidator
    {
        /// <summary>
        /// Validate display version format: yyyy.M.d.S where S is seconds since midnight (0..86399), max 5 digits.
        /// </summary>
        public static bool IsValid(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return false;

            var pattern = "^(\\d{4})\\.(?:[1-9]|1[0-2])\\.(?:[1-9]|[12]\\d|3[01])\\.(\\d{1,5})$";
            var m = Regex.Match(v, pattern);
            if (!m.Success) return false;

            if (!int.TryParse(m.Groups[1].Value, out _)) return false; // year parsed by regex

            if (!int.TryParse(m.Groups[2].Value, out var seconds))
                return false;

            return seconds >= 0 && seconds <= 86399;
        }
    }
}
