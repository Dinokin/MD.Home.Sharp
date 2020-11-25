using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace MD.Home.Server.Others
{
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public sealed class Token
    {
        public string ClientId { get; }
        
        public string Ip { get; }
        
        public string Hash { get; }
        
        [JsonPropertyName("expires")]
        public DateTime ExpirationDate { get; }

        public Token(string clientId, string ip, string hash, DateTime expirationDate)
        {
            ClientId = clientId;
            Ip = ip;
            Hash = hash;
            ExpirationDate = expirationDate;
        }
    }
}