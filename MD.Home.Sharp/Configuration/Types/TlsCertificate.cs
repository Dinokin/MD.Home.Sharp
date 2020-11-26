using System;
using System.Diagnostics.CodeAnalysis;

namespace MD.Home.Sharp.Configuration.Types
{
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    public sealed class TlsCertificate
    {
        public string Certificate { get; }
        public string PrivateKey { get; }
        public string CreatedAt { get; }

        public TlsCertificate(string certificate, string privateKey, string createdAt)
        {
            Certificate = certificate;
            PrivateKey = privateKey;
            CreatedAt = createdAt;
        }
        
        public static bool operator ==(TlsCertificate? left, TlsCertificate? right) => left?.Certificate == right?.Certificate && left?.PrivateKey == right?.PrivateKey && left?.CreatedAt == right?.CreatedAt;
        
        public static bool operator !=(TlsCertificate? left, TlsCertificate? right) => !(left == right);
        
        public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is TlsCertificate other && this == other;

        public override int GetHashCode() => HashCode.Combine(Certificate, PrivateKey, CreatedAt);
    }
}