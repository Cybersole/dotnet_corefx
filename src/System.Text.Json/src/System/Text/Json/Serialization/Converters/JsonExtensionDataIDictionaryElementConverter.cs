// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.Json.Serialization.Converters
{
    internal sealed class JsonExtensionDataIDictionaryElementConverter : JsonDictionaryConverter<IDictionary<string, JsonElement>>
    {
        internal override bool OnTryRead(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options, ref ReadStack state, ref IDictionary<string, JsonElement> value)
        {
            // Reading is not handled through a converter.
            Debug.Assert(false);

            value = default;
            return true;
        }

        internal override sealed bool OnTryWrite(Utf8JsonWriter writer, IDictionary<string, JsonElement> value, JsonSerializerOptions options, ref WriteStack state)
        {
            IEnumerator<KeyValuePair<string, JsonElement>> enumerator = value.GetEnumerator();
            JsonConverter<JsonElement> converter = JsonSerializerOptions.GetJsonElementConverter();

            if (state.Current.CollectionEnumerator != null)
            {
                enumerator = (IEnumerator<KeyValuePair<string, JsonElement>>)state.Current.CollectionEnumerator;
            }

            while (enumerator.MoveNext())
            {
                string key = enumerator.Current.Key;
                writer.WritePropertyName(key);

                JsonElement element = enumerator.Current.Value;
                if (!converter.TryWrite(writer, element, options, ref state))
                {
                    state.Current.KeyName = key;
                    state.Current.CollectionEnumerator = enumerator;
                    return false;
                }
            }

            return true;
        }
    }
}
