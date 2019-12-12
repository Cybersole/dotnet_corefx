// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Text.Json.Serialization.Converters
{
    internal abstract class JsonDictionaryDefaultConverter<TCollection, TValue> : JsonDictionaryConverter<TCollection>
    {
        private Type _elementType = typeof(TValue);

        internal override Type ElementType => _elementType;

        internal override sealed bool OnTryRead(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options, ref ReadStack state, ref TCollection value)
        {
            // Read StartObject.
            if (!state.Current.ProcessedStartToken)
            {
                state.Current.ProcessedStartToken = true;

                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(TypeToConvert);
                }

                CreateCollection(ref state);
            }

            JsonConverter<TValue> elementConverter = (JsonConverter<TValue>)state.Current.JsonClassInfo.ElementClassInfo.PolicyProperty.ConverterBase;

            while (true)
            {
                if (state.Current.ProcessedReadName == false)
                {
                    state.Current.ProcessedReadName = true;

                    if (!reader.Read())
                    {
                        return false;
                    }
                }

                // Determine the property.
                if (state.Current.ProcessedName == false)
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        break;
                    }

                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(TypeToConvert);
                    }

                    state.Current.ProcessedName = true;

                    state.Current.KeyName = reader.GetString();
                }

                if (state.Current.ProcessedReadValue == false)
                {
                    state.Current.ProcessedReadValue = true;

                    if (!elementConverter.ReadWithReadAhead(ref reader, ref state))
                    {
                        return false;
                    }
                }

                if (state.Current.ProcessedValue == false)
                {
                    // Obtain the CLR value from the JSON.
                    TValue element = default;

                    bool success = elementConverter.TryRead(ref reader, typeof(TValue), options, ref state, ref element);
                    if (!success)
                    {
                        return false;
                    }

                    Add(element, options, ref state);
                    state.Current.ProcessedValue = true;
                    state.Current.EndElement();
                }
            }

            ConvertCollection(ref state);

            value = (TCollection)state.Current.ReturnValue;

            return true;
        }

        internal override sealed bool OnTryWrite(Utf8JsonWriter writer, TCollection dictionary, JsonSerializerOptions options, ref WriteStack state)
        {
            writer.WriteStartObject(); //todo: verify didn't write

            bool success = OnWriteResume(writer, dictionary, options, ref state);
            if (success)
            {
                writer.WriteEndObject();
            }

            return success;
        }

        protected abstract void Add(TValue value, JsonSerializerOptions options, ref ReadStack state);

        protected JsonConverter<TValue> GetValueConverter(ref WriteStack state)
        {
            JsonConverter<TValue> converter = (JsonConverter<TValue>)state.Current.JsonClassInfo.ElementClassInfo?.PolicyProperty.ConverterBase;
            if (converter == null)
            {
                state.Current.JsonPropertyInfo.ThrowCollectionNotSupportedException();
            }

            return converter;
        }

        protected string GetKeyName(string key, ref WriteStack state, JsonSerializerOptions options)
        {
            if (options.DictionaryKeyPolicy != null)
            {
                key = options.DictionaryKeyPolicy.ConvertName(key);

                if (key == null)
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializerDictionaryKeyNull(options.DictionaryKeyPolicy.GetType());
                }
            }

            return key;
        }

        protected virtual void CreateCollection(ref ReadStack state) { }
        protected virtual void ConvertCollection(ref ReadStack state) { }
        protected abstract bool OnWriteResume(Utf8JsonWriter writer, TCollection dictionary, JsonSerializerOptions options, ref WriteStack state);
    }
}
