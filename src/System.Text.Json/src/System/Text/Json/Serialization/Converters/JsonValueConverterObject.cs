// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Text.Json.Serialization.Converters
{
    internal sealed class JsonConverterObject : JsonResumableConverter<object>
    {
        internal override bool OnTryRead(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options, ref ReadStack state, ref object value)
        {
            JsonConverter<JsonElement> converter = JsonSerializerOptions.GetJsonElementConverter();

            JsonElement jsonElement = default;
            bool success = converter.TryRead(ref reader, typeToConvert, options, ref state, ref jsonElement);
            if (success)
            {
                value = jsonElement;
            }

            return success;
        }

        internal override bool OnTryWrite(Utf8JsonWriter writer, object value, JsonSerializerOptions options, ref WriteStack state)
        {
            bool success;

            if (value == null)
            {
                writer.WriteNullValue();
                success = true;
            }
            else
            {
                Type type = value.GetType();
                if (type != typeof(object))
                {
                    // Forward to the correct converter.
                    JsonConverter converter = options.GetConverter(type, ref state);
                    success = converter.TryWriteAsObject(writer, value, options, ref state);
                }
                else
                {
                    // Avoid recursion when System.Object is newed up directly.
                    writer.WriteStartObject();
                    writer.WriteEndObject();
                    success = true;
                }
            }

            return success;
        }
    }
}
