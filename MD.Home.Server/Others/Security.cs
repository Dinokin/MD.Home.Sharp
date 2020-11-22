using System;
using System.Text.RegularExpressions;

namespace MD.Home.Server.Others
{
    public static class Security
    {
        private const string CertificateHeader = "-----BEGIN CERTIFICATE-----";
        private const string CertificateFooter = "-----END CERTIFICATE-----";
        private const string PrivateKeyHeader = "-----BEGIN RSA PRIVATE KEY-----";
        private const string PrivateKeyFooter = "-----END RSA PRIVATE KEY-----";

        public static byte[] GetCertificateBytesFromBase64(string certificate)
        {
            var filteredCert = certificate.Replace(CertificateHeader, string.Empty);
            filteredCert = filteredCert.Replace(CertificateFooter, string.Empty);
            filteredCert = filteredCert.Replace(PrivateKeyHeader, string.Empty);
            filteredCert = filteredCert.Replace(PrivateKeyFooter, string.Empty);

            filteredCert = Regex.Replace(filteredCert, "[^a-zA-Z0-9+/]", string.Empty);

            return Convert.FromBase64String(filteredCert);
        }
    }
}