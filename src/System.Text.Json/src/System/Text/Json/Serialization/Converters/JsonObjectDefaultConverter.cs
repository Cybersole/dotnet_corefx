// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Text.Json.Serialization.Converters
{
    internal sealed class JsonObjectDefaultConverter<T> : JsonObjectConverter<T>
    {
        internal override bool OnTryRead(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options, ref ReadStack state, ref T value)
        {
            object obj;

            if (!state.SupportContinuation)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(TypeToConvert);
                }

                if (state.Current.JsonClassInfo.CreateObject == null)
                {
                    ThrowHelper.ThrowNotSupportedException_DeserializeCreateObjectDelegateIsNull(state.Current.JsonClassInfo.Type);
                }

                obj = state.Current.JsonClassInfo.CreateObject();
                state.Current.ReturnValue = obj;

                // Read all properties.
                while (true)
                {
                    // Lookup the property.
                    reader.Read();

                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        break;
                    }

                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(TypeToConvert);
                    }

                    bool useExtensionProperty = JsonSerializer.HandlePropertyName(ref reader, options, ref state);

                    // Skip the property if not found.
                    if (state.Current.SkipProperty)
                    {
                        reader.TrySkip();
                        state.Current.EndProperty();
                        continue;
                    }

                    // Set the property value.
                    reader.Read();

                    if (!useExtensionProperty)
                    {
                        state.Current.JsonPropertyInfo.ReadJsonAndSetMember(obj, ref state, ref reader);
                    }
                    else
                    {
                        state.Current.JsonPropertyInfo.ReadJsonAndAddExtensionProperty(obj, ref state, ref reader);
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
                }

                // Create the object.
                if (state.Current.ReturnValue == null)
                {
                    if (state.Current.JsonClassInfo.CreateObject == null)
                    {
                        ThrowHelper.ThrowNotSupportedException_DeserializeCreateObjectDelegateIsNull(state.Current.JsonClassInfo.Type);
                    }

                    state.Current.ReturnValue = state.Current.JsonClassInfo.CreateObject();
                }

                obj = state.Current.ReturnValue;

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
                        if (JsonSerializer.HandlePropertyName(ref reader, options, ref state))
                        {
                            state.Current.UseExtensionProperty = true;
                        }
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

                        if (!state.Current.UseExtensionProperty)
                        {
                            if (!SingleValueReadWithReadAhead(jsonPropertyInfo.ConverterBase.ClassType, ref reader, ref state))
                            {
                                return false;
                            }
                        }
                        else
                        {
                            // The actual converter is JsonElement, so force a read-ahead.
                            if (!SingleValueReadWithReadAhead(ClassType.Value, ref reader, ref state))
                            {
                                return false;
                            }
                        }
                    }

                    if (state.Current.ProcessedValue == false)
                    {
                        // Obtain the CLR value from the JSON and set the member.
                        if (!state.Current.UseExtensionProperty)
                        {
                            if (!jsonPropertyInfo.ReadJsonAndSetMember(obj, ref state, ref reader))
                            {
                                return false;
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
            }

            // Check if we are trying to build the sorted cache.
            if (state.Current.PropertyRefCache != null)
            {
                state.Current.JsonClassInfo.UpdateSortedPropertyCache(ref state.Current);
            }

            value = (T)obj;

            return true;
        }

        internal override bool OnTryWrite(Utf8JsonWriter writer, T value, JsonSerializerOptions options, ref WriteStack state)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return true;
            }

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
