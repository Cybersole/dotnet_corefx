// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace System.Text.Json.Serialization.Converters
{
    internal sealed class JsonIDictionaryOfStringTValueConverter<TCollection, TValue> : JsonDictionaryDefaultConverter<TCollection, TValue> where TCollection : IDictionary<string, TValue>
    {
        protected override void CreateCollection(ref ReadStack state)
        {
            JsonClassInfo classInfo = state.Current.JsonClassInfo;
            Type type = state.Current.JsonClassInfo.Type;
            if (type.IsAbstract || type.IsInterface)
            {
                state.Current.ReturnValue = new Dictionary<string, TValue>();
            }
            else
            {
                state.Current.ReturnValue = classInfo.CreateObject();
            }
        }

        protected override void Add(TValue value, JsonSerializerOptions options, ref ReadStack state)
        {
            string key = state.Current.KeyName;
            ((TCollection)state.Current.ReturnValue)[key] = value;
        }

        protected override bool OnWriteResume(Utf8JsonWriter writer, TCollection value, JsonSerializerOptions options, ref WriteStack state)
        {
            JsonConverter<TValue> converter = GetValueConverter(ref state);

            IEnumerator<KeyValuePair<string, TValue>> enumerator;
            if (state.Current.CollectionEnumerator == null)
            {
                enumerator = value.GetEnumerator();
            }
            else
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
