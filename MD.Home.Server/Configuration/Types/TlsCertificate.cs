using System;
using System.Diagnostics.CodeAnalysis;

namespace MD.Home.Server.Configuration.Types
{
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    public sealed class TlsCertificate
    {
        public string Certificate { get; }
        public string PrivateKey { get; }
        public DateTime CreatedAt { get; }

        public TlsCertificate(string certificate, string privateKey, DateTime createdAt)
        {
            Certificate = certificate;
            PrivateKey = privateKey;
            CreatedAt = createdAt;
        }
    }
}