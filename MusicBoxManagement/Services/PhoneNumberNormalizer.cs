using System;
using System.Text;

namespace MusicBoxManagement.Services
{
    public static class PhoneNumberNormalizer
    {
        public static bool TryNormalize(string input, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var cleaned = new StringBuilder(input.Length);
            foreach (var character in input)
            {
                if (char.IsWhiteSpace(character) || character == '.' || character == '-') continue;
                cleaned.Append(character);
            }

            var value = cleaned.ToString();
            if (value.StartsWith("+84", StringComparison.Ordinal)) value = "0" + value.Substring(3);
            else if (value.StartsWith("84", StringComparison.Ordinal)) value = "0" + value.Substring(2);

            if (value.Length != 10 || value[0] != '0') return false;
            foreach (var character in value)
                if (character < '0' || character > '9') return false;

            normalized = value;
            return true;
        }
    }
}
