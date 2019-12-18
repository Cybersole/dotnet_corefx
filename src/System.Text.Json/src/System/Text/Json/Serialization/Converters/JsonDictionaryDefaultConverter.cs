// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace System.Text.Json.Serialization.Converters
{
    internal abstract class JsonDictionaryDefaultConverter<TCollection, TValue> : JsonDictionaryConverter<TCollection>
    {
        private Type _elementType = typeof(TValue);

        internal override Type ElementType => _elementType;

        internal override sealed bool OnTryRead(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options, ref ReadStack state, ref TCollection value)
        {
            if (!state.SupportContinuation)
            {
                // Fast path that avoids maintaining state variables.

                // Read StartObject.
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(TypeToConvert);
                }

                CreateCollection(ref state);

                JsonConverter<TValue> elementConverter = GetElementConverter(ref state);

                if (elementConverter.ClassType == ClassType.Value && elementConverter.IsInternalConverter)
                {
                    // Fast path that avoids validation and extra indirection.
                    while (true)
                    {
                        // Read the key name.
                        reader.Read();

                        if (reader.TokenType == JsonTokenType.EndObject)
                        {
                            break;
                        }

                        if (reader.TokenType != JsonTokenType.PropertyName)
                        {
                            ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(TypeToConvert);
                        }

                        state.Current.KeyName = reader.GetString();

                        // Read the value.
                        reader.Read();

                        TValue element = elementConverter.Read(ref reader, _elementType, options);
                        Add(element, options, ref state);
                    }
                }
                else
                {
                    while (true)
                    {
                        // Read the key name.
                        reader.Read();

                        if (reader.TokenType == JsonTokenType.EndObject)
                        {
                            break;
                        }

                        if (reader.TokenType != JsonTokenType.PropertyName)
                        {
                            ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(TypeToConvert);
                        }

                        state.Current.KeyName = reader.GetString();

                        // Read the value.
                        reader.Read();

                        TValue element = default;
                        elementConverter.TryRead(ref reader, _elementType, options, ref state, ref element);

                        Add(element, options, ref state);
                    }
                }
            }
            else
            {
                // Read StartObject.
                if (!state.Current.ProcessedStartToken)
                {
                    if (reader.TokenType != JsonTokenType.StartObject)
                    {
                        ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(TypeToConvert);
                    }

                    state.Current.ProcessedStartToken = true;
                    CreateCollection(ref state);
                }

                JsonConverter<TValue> elementConverter = GetElementConverter(ref state);

                while (true)
                {
                    if (state.Current.ProcessedReadName == false)
                    {
                        state.Current.ProcessedReadName = true;

                        // Read the key name.
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

                        if (!SingleValueReadWithReadAhead(elementConverter.ClassType, ref reader, ref state))
                        {
                            return false;
                        }
                    }

                    if (state.Current.ProcessedValue == false)
                    {
                        TValue element = default;

                        // Read the value.
                        bool success = elementConverter.TryRead(ref reader, _elementType, options, ref state, ref element);
                        if (!success)
                        {
                            return false;
                        }

                        Add(element, options, ref state);
                        state.Current.ProcessedValue = true;
                        state.Current.EndElement();
                    }
                }
            }

            ConvertCollection(ref state);
            value = (TCollection)state.Current.ReturnValue;
            return true;
        }

        internal override sealed bool OnTryWrite(Utf8JsonWriter writer, TCollection dictionary, JsonSerializerOptions options, ref WriteStack state)
        {
            bool success;

            if (dictionary == null)
            {
                writer.WriteNullValue();
                success = true;
            }
            else
            {
                writer.WriteStartObject(); //todo: verify didn't write

                success = OnWriteResume(writer, dictionary, options, ref state);
                if (success)
                {
                    writer.WriteEndObject();
                }
            }

            return success;
        }

        protected abstract void Add(TValue value, JsonSerializerOptions options, ref ReadStack state);

        protected static JsonConverter<TValue> GetElementConverter(ref ReadStack state)
        {
            JsonConverter<TValue> converter = state.Current.JsonClassInfo.ElementClassInfo?.PolicyProperty.ConverterBase as JsonConverter<TValue>;
            if (converter == null)
            {
                state.Current.JsonPropertyInfo.ThrowCollectionNotSupportedException();
            }

            return converter;
        }

        protected JsonConverter<TValue> GetValueConverter(ref WriteStack state)
        {
            JsonConverter<TValue> converter = (JsonConverter<TValue>)state.Current.JsonClassInfo.ElementClassInfo?.PolicyProperty.ConverterBase;
            if (converter == null)
            {
                state.Current.JsonPropertyInfo.ThrowCollectionNotSupportedException();
            }

            return converter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected string GetKeyName(string key, ref WriteStack state, JsonSerializerOptions options)
        {
            if (options.DictionaryKeyPolicy != null && !state.Current.IgnoreDictionaryKeyPolicy)
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
    }
}
