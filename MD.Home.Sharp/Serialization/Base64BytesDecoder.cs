using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;

namespace MD.Home.Sharp.Serialization
{
    internal class Base64BytesDecoder : JsonConverter<byte[]>
    {
        public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try
            {
                var base64String = reader.GetString();

                return base64String == null ? null : WebEncoders.Base64UrlDecode(base64String);
            }
            catch
            {
                throw new JsonException();
            }
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        {
            try
            {
                writer.WriteBase64StringValue(value);
            }
            catch
            {
                throw new JsonException();
            }
        }
    }
}