using System.Collections.Immutable;

namespace MD.Home.Sharp.Others
{
    internal static class Constants
    {
        public const string ServerAddress = "https://api.mangadex.network/";
        public const string SettingsFile = "settings.json";
        public const string CacheFile = "cache.db";
        public const ushort ClientBuild = 19;

        public static readonly ImmutableArray<ushort> ReservedPorts = ImmutableArray.Create<ushort>(
            1, 7, 9, 11, 13, 15, 17, 19, 20, 21, 22, 23, 25, 37, 42, 43, 53, 77, 79, 87, 95,
            101, 102, 103, 104, 109, 110, 111, 113, 115, 117, 119, 123, 135, 139, 143, 179, 389, 465, 512,
            513, 514, 515, 526, 530, 531, 532, 540, 556, 563, 587, 601, 636, 993, 995, 2049, 4045, 6000);
    }
}