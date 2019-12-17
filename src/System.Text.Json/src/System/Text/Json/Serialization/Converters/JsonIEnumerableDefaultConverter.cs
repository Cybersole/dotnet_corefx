// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Text.Json.Serialization.Converters
{
    internal abstract class JsonIEnumerableDefaultConverter<TCollection, TElement> : JsonArrayConverter<TCollection, TElement>
    {
        internal override bool OnTryRead(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options, ref ReadStack state, ref TCollection value)
        {
            JsonConverter<TElement> elementConverter = GetElementConverter(ref state);

            // Read StartArray.
            if (!state.Current.ProcessedStartToken)
            {
                if (reader.TokenType != JsonTokenType.StartArray)
                {
                    ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(TypeToConvert);
                }

                state.Current.ProcessedStartToken = true;
                CreateCollection(ref state);
            }

            // Read all items.
            while (true)
            {
                if (state.Current.ProcessedReadValue == false)
                {
                    state.Current.ProcessedReadValue = true;

                    if (!elementConverter.SingleValueReadWithReadAhead(ref reader, ref state))
                    {
                        return false;
                    }
                }

                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    state.Current.ProcessedEndToken = true;
                    break;
                }

                if (state.Current.ProcessedValue == false)
                {
                    // Obtain the CLR value from the JSON and apply to the object.
                    TElement element = default;

                    bool success = elementConverter.TryRead(ref reader, typeof(TElement), options, ref state, ref element);
                    if (!success)
                    {
                        return false;
                    }

                    state.Current.ProcessedValue = true;
                    Add(element, ref state);

                    state.Current.EndElement();
                }
            }

            ConvertCollection(ref state);
            value = (TCollection)state.Current.ReturnValue;
            return true;
        }

        internal override sealed bool OnTryWrite(Utf8JsonWriter writer, TCollection value, JsonSerializerOptions options, ref WriteStack state)
        {
            bool success;

            if (value == null)
            {
                writer.WriteNullValue();
                success = true;
            }
            else
            {
                if (!state.Current.ProcessedStartToken)
                {
                    writer.WriteStartArray();
                    state.Current.ProcessedStartToken = true;
                }

                success = OnWriteResume(writer, value, options, ref state);
                if (success)
                {
                    writer.WriteEndArray();
                }
            }

            return success;
        }

        protected abstract void Add(TElement value, ref ReadStack state);

        protected static JsonConverter<TElement> GetElementConverter(ref ReadStack state)
        {
            JsonConverter<TElement> converter = state.Current.JsonClassInfo.ElementClassInfo.PolicyProperty.ConverterBase as JsonConverter<TElement>;
            if (converter == null)
            {
                state.Current.JsonPropertyInfo.ThrowCollectionNotSupportedException();
            }

            return converter;
        }

        protected static JsonConverter<TElement> GetElementConverter(ref WriteStack state)
        {
            JsonConverter<TElement> converter = state.Current.JsonClassInfo.ElementClassInfo?.PolicyProperty.ConverterBase as JsonConverter<TElement>;
            if (converter == null)
            {
                state.Current.JsonPropertyInfo.ThrowCollectionNotSupportedException();
            }

            return converter;
        }

        protected virtual void CreateCollection(ref ReadStack state) { }
        protected virtual void ConvertCollection(ref ReadStack state) { }
        protected abstract bool OnWriteResume(Utf8JsonWriter writer, TCollection value, JsonSerializerOptions options, ref WriteStack state);
    }
}
