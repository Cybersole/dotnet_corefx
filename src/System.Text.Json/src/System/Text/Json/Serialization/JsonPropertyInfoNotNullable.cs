// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json.Serialization.Converters;

namespace System.Text.Json
{
    /// <summary>
    /// Represents a strongly-typed property that is not a <see cref="Nullable{T}"/>.
    /// </summary>
    internal sealed class JsonPropertyInfoNotNullable<TClass, TDeclaredProperty, TRuntimeProperty, TConverter> :
        JsonPropertyInfoCommon<TClass, TDeclaredProperty, TRuntimeProperty, TConverter>
        where TConverter : TDeclaredProperty
    {
        public override bool GetMemberAndWriteJson(object obj, ref WriteStack state, Utf8JsonWriter writer)
        {
            Debug.Assert(EscapedName.HasValue);

            bool success;

            TConverter value = (TConverter)Get(obj);

            if (value == null)
            {
                if (!IgnoreNullValues)
                {
                    writer.WriteNull(EscapedName.Value);
                }

                success = true;
            }
            else
            {
                writer.WritePropertyName(EscapedName.Value);
                success = Converter.TryWrite(writer, value, Options, ref state);
            }

            return success;
        }

        public override bool GetMemberAndWriteJsonExtensionData(object obj, ref WriteStack state, Utf8JsonWriter writer)
        {
            bool success;
            TConverter value = (TConverter)Get(obj);

            if (value == null)
            {
                success = true;
            }
            else
            {
                success = Converter.TryWriteDataExtensionProperty(writer, value, Options, ref state);
            }

            return success;
        }

        public override bool ReadJsonAndSetMember(object obj, ref ReadStack state, ref Utf8JsonReader reader)
        {
            bool success;

            if (reader.TokenType == JsonTokenType.Null && !Converter.ConvertNullValue)
            {
                if (!IgnoreNullValues)
                {
                    TConverter value = default;
                    Set(obj, value);
                }

                success = true;
            }
            else
            {
                TConverter value = default;
                success = Converter.TryRead(ref reader, RuntimePropertyType, Options, ref state, ref value);
                if (success)
                {
                    if (reader.TokenType != JsonTokenType.Null || !IgnoreNullValues || value != null)
                    {
                        Set(obj, value);
                    }
                }
            }

            return success;
        }
    }
}
