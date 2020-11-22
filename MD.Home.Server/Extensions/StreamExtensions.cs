using System.IO;

namespace MD.Home.Server.Extensions
{
    public static class StreamExtensions
    {
        public static byte[] GetBytes(this Stream source)
        {
            var memoryStream = new MemoryStream();
            
            source.CopyTo(memoryStream);

            return memoryStream.ToArray();
        }
    }
}