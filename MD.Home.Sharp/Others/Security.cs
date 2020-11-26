using System;
using System.Text.RegularExpressions;

namespace MD.Home.Sharp.Others
{
    public static class Security
    {
        public enum InputType
        {
            Certificate,
            PrivateKey
        }
        
        private const string CertificateHeader = "-----BEGIN CERTIFICATE-----";
        private const string CertificateFooter = "-----END CERTIFICATE-----";
        private const string PrivateKeyHeader = "-----BEGIN RSA PRIVATE KEY-----";
        private const string PrivateKeyFooter = "-----END RSA PRIVATE KEY-----";

        public static byte[] GetCertificateBytesFromBase64(string input, InputType type)
        {
            string filteredInput = type switch
            {
                InputType.Certificate => input.Replace(CertificateHeader, string.Empty).Replace(CertificateFooter, string.Empty),
                InputType.PrivateKey => input.Replace(PrivateKeyHeader, string.Empty).Replace(PrivateKeyFooter, string.Empty),
                _ => throw new InvalidOperationException($"Unknown input type: {type}")
            };

            filteredInput = Regex.Replace(filteredInput, "[^a-zA-Z0-9+/]", string.Empty);

            return Convert.FromBase64String(filteredInput);
        }
    }
}