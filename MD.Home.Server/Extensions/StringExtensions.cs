using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;

namespace MD.Home.Server.Extensions
{
    public static class StringExtensions
    {
        public static bool IsValidSecret(this string source) => !string.IsNullOrWhiteSpace(source) && Regex.IsMatch(source, "^[a-zA-Z0-9]{52}$");

        public static bool IsImageMimeType(this string? source) => !string.IsNullOrWhiteSpace(source) && Regex.IsMatch(source, "^image/");

        public static byte[] DecodeFromBase64Url(this string source) => string.IsNullOrWhiteSpace(source) ? Array.Empty<byte>() : WebEncoders.Base64UrlDecode(source);

        public static Guid GetHashAsGuid(this string source)
        {
            using var hasher = MD5.Create();
            var hashBytes = hasher.ComputeHash(Encoding.UTF8.GetBytes(source));
            var sb = new StringBuilder();
            
            foreach (var b in hashBytes)
                sb.Append(b.ToString("X2"));
            
            return Guid.Parse(sb.ToString().ToLowerInvariant());
        }
    }
}