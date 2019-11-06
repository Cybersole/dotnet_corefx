// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace System.Text.Json.Serialization.Converters
{
    internal sealed class JsonDictionaryOfStringTValueConverter<TCollection, TValue> : JsonDictionaryDefaultConverter<TCollection, TValue> where TCollection : Dictionary<string, TValue>, new()
    {
        protected override void CreateCollection(ref ReadStack state)
        {
            state.Current.ReturnValue = new TCollection();
        }

        protected override void Add(TValue value, JsonSerializerOptions options, ref ReadStack state)
        {
            string key = state.Current.KeyName;
            ((TCollection)state.Current.ReturnValue)[key] = value;
        }

        protected override bool OnWriteResume(Utf8JsonWriter writer, TCollection value, JsonSerializerOptions options, ref WriteStack state)
        {
            Dictionary<string, TValue>.Enumerator enumerator = value.GetEnumerator();
            JsonConverter<TValue> converter = GetValueConverter(ref state);

            if (state.Current.CollectionEnumerator != null)
            {
                enumerator = (Dictionary<string, TValue>.Enumerator)state.Current.CollectionEnumerator;
            }

            while (enumerator.MoveNext())
            {
                TValue element = enumerator.Current.Value;
                string key = GetKeyName(enumerator.Current.Key, ref state, options);
                writer.WritePropertyName(key);
                if (!converter.TryWrite(writer, element, options, ref state))
                {
                    state.Current.CollectionEnumerator = enumerator;
                    return false;
                }
            }

            return true;
        }
    }
}
