using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MD.Home.Sharp.Extensions
{
    internal static class StringExtensions
    {
        public static bool IsValidSecret(this string source) => !string.IsNullOrWhiteSpace(source) && Regex.IsMatch(source, "^[a-zA-Z0-9]{52}$");

        public static bool IsImageMimeType(this string? source) => !string.IsNullOrWhiteSpace(source) && Regex.IsMatch(source, "^image/");

        public static string? RemoveToken(this string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return source;

            return source.Contains("/data") ? source.Substring(source.IndexOf("/data", StringComparison.InvariantCulture)) : source;
        }
        
        public static string GetMd5Hash(this string source)
        {
            using var hasher = MD5.Create();
            var hashBytes = hasher.ComputeHash(Encoding.UTF8.GetBytes(source));
            var sb = new StringBuilder();
            
            foreach (var b in hashBytes)
                sb.Append(b.ToString("X"));

            return sb.ToString();
        }
    }
}