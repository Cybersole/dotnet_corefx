// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Text.Json.Serialization.Converters
{
    internal sealed class JsonObjectDefaultConverter<T> : JsonObjectConverter<T>
    {
        internal override bool OnTryRead(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options, ref ReadStack state, ref T value)
        {
            // Read StartObject.
            if (!state.Current.ProcessedStartToken)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(TypeToConvert);
                }

                state.Current.ProcessedStartToken = true;
            }

            // Create the object.
            if (value == null)
            {
                if (state.Current.JsonClassInfo.CreateObject == null)
                {
                    ThrowHelper.ThrowNotSupportedException_DeserializeCreateObjectDelegateIsNull(state.Current.JsonClassInfo.Type);
                }

                value = (T)state.Current.JsonClassInfo.CreateObject();
                state.Current.ReturnValue = value;
            }

            // Read all properties.
            while (true)
            {
                // Determine the property.
                if (state.Current.ProcessedReadName == false)
                {
                    state.Current.ProcessedReadName = true;

                    if (!reader.Read())
                    {
                        return false;
                    }
                }

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
                    JsonSerializer.HandlePropertyName(ref reader, options, ref state);
                }

                JsonPropertyInfo jsonPropertyInfo = state.Current.JsonPropertyInfo;

                if (state.Current.ProcessedReadValue == false)
                {
                    if (state.Current.SkipProperty)
                    {
                        if (!reader.TrySkip())
                        {
                            return false;
                        }

                        state.Current.EndProperty();
                        continue;
                    }

                    state.Current.ProcessedReadValue = true;

                    if (!jsonPropertyInfo.ConverterBase.ReadWithReadAhead(ref reader, ref state))
                    {
                        return false;
                    }
                }

                if (state.Current.ProcessedValue == false)
                {
                    state.Current.ProcessedValue = true;

                    // Obtain the CLR value from the JSON and set the member.
                    object obj = value;

                    if (!state.Current.UseExtensionProperty)
                    {
                        if (!jsonPropertyInfo.ReadJsonAndSetMember(obj, ref state, ref reader))
                        {
                            value = (T)obj;
                            return false;
                        }
                        else
                        {
                            value = (T)obj;
                        }
                    }
                    else
                    {
                        if (!jsonPropertyInfo.ReadJsonAndAddExtensionProperty(obj, ref state, ref reader))
                        {
                            // No need to set 'value' here since JsonElement must be read in full.
                            return false;
                        }
                    }

                    state.Current.EndProperty();
                }
            }

            // Check if we are trying to build the sorted cache.
            if (state.Current.PropertyRefCache != null)
            {
                state.Current.JsonClassInfo.UpdateSortedPropertyCache(ref state.Current);
            }

            return true;
        }

        internal override bool OnTryWrite(Utf8JsonWriter writer, T value, JsonSerializerOptions options, ref WriteStack state)
        {
            if (!state.Current.ProcessedStartToken)
            {
                writer.WriteStartObject();
                state.Current.ProcessedStartToken = true;
            }

            state.Current.CurrentValue = value;


            JsonPropertyInfo dataExtensionProperty = state.Current.JsonClassInfo.DataExtensionProperty;

            while (state.Current.NextProperty())
            {
                JsonPropertyInfo jsonPropertyInfo = state.Current.JsonClassInfo.PropertyCacheArray[state.Current.PropertyEnumeratorIndex - 1];
                state.Current.JsonPropertyInfo = jsonPropertyInfo;

                if (jsonPropertyInfo.ShouldSerialize)
                {
                    if (jsonPropertyInfo == dataExtensionProperty)
                    {
                        if (!jsonPropertyInfo.GetMemberAndWriteJsonExtensionData(value, ref state, writer))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (!jsonPropertyInfo.GetMemberAndWriteJson(value, ref state, writer))
                        {
                            return false;
                        }
                    }
                }
            }

            writer.WriteEndObject();
            return true;
        }
    }
}
