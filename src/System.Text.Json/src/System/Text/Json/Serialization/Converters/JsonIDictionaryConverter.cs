// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;

namespace System.Text.Json.Serialization.Converters
{
    internal sealed class JsonIDictionaryConverter : JsonDictionaryDefaultConverter<IDictionary, object>
    {
        protected override void CreateCollection(ref ReadStack state)
        {
            JsonClassInfo classInfo = state.Current.JsonClassInfo;
            Type type = state.Current.JsonClassInfo.Type;
            if (type.IsAbstract || type.IsInterface)
            {
                state.Current.ReturnValue = new Dictionary<string, object>();
            }
            else
            {
                state.Current.ReturnValue = classInfo.CreateObject();
            }
        }

        protected override void Add(object value, JsonSerializerOptions options, ref ReadStack state)
        {
            string key = state.Current.KeyName;
            ((IDictionary)state.Current.ReturnValue)[key] = value;
        }

        protected internal override bool OnWriteResume(Utf8JsonWriter writer, IDictionary value, JsonSerializerOptions options, ref WriteStack state)
        {
            JsonConverter<object> converter = GetValueConverter(ref state);

            IDictionaryEnumerator enumerator;
            if (state.Current.CollectionEnumerator == null)
            {
                enumerator = value.GetEnumerator();
            }
            else
            {
                enumerator = (IDictionaryEnumerator)state.Current.CollectionEnumerator;
            }

            while (enumerator.MoveNext())
            {
                if (!(enumerator.Key is string key))
                {
                    // todo: or throw NotSupportedException?
                    ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(state.Current.JsonPropertyInfo.RuntimePropertyType);
                    return false;
                }

                key = GetKeyName(key, ref state, options);
                writer.WritePropertyName(key);

                object element = enumerator.Value;
                if (!converter.TryWriteAsObject(writer, element, options, ref state))
                {
                    state.Current.CollectionEnumerator = enumerator;
                    return false;
                }
            }

            return true;
        }

        internal override Type RuntimeType
        {
            get
            {
                if (TypeToConvert.IsAbstract || TypeToConvert.IsInterface)
                {
                    return typeof(Dictionary<string, object>);
                }

                return TypeToConvert;
            }
        }
    }
}
