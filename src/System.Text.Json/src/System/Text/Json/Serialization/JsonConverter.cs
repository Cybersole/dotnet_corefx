// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// Converts an object or value to or from JSON.
    /// </summary>
    public abstract partial class JsonConverter
    {
        internal JsonConverter() { }

        /// <summary>
        /// Determines whether the type can be converted.
        /// </summary>
        /// <param name="typeToConvert">The type is checked as to whether it can be converted.</param>
        /// <returns>True if the type can be converted, false otherwise.</returns>
        public abstract bool CanConvert(Type typeToConvert);

        internal abstract ClassType ClassType { get; }

        // Whether the converter should handle the null value.
        internal virtual bool HandleNullValue
        {
            get
            {
                // Allow a converter that can't be null to return a null value representation, such as JsonElement or Nullable<>.
                // In other cases, this will likely cause an JsonException in the converter.
                return TypeToConvert.IsValueType;
            }
        }

        internal abstract Type ElementType { get; }

        internal bool IsInternalConverter { get; set; }

        internal void Push(ref ReadStack state)
        {
            JsonPropertyInfo jsonPropertyInfo;
            if (state.Current.JsonClassInfo.ClassType == ClassType.Object)
            {
                jsonPropertyInfo = state.Current.JsonPropertyInfo;
            }
            else
            {
                jsonPropertyInfo = state.Current.JsonClassInfo.ElementClassInfo.PolicyProperty;
            }

            state.Push(jsonPropertyInfo);
        }

        internal void Push(ref WriteStack state, object nextValue)
        {
            JsonPropertyInfo jsonPropertyInfo;
            if (state.Current.JsonClassInfo.ClassType == ClassType.Object)
            {
                jsonPropertyInfo = state.Current.JsonPropertyInfo;
            }
            else
            {
                jsonPropertyInfo = state.Current.JsonElementPropertyInfo;
            }

            state.Push(jsonPropertyInfo);
            state.Current.CurrentValue = nextValue;

        }

        // For polymorphic cases, the concrete type to create.
        internal virtual Type RuntimeType => TypeToConvert;

        // This is used internally to quickly determine the type being converted for JsonConverter<T>.
        internal abstract Type TypeToConvert { get; }

        internal bool TryReadAsObject(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options, ref ReadStack state, ref object value)
        {
            if (reader.TokenType == JsonTokenType.Null && !HandleNullValue)
            {
                return true;
            }

            if (ClassType != ClassType.Value)
            {
                Push(ref state);
            }

            bool success;

            // For performance on release build, don't verify converter correctness for internal converters.
            if (IsInternalConverter)
            {
#if DEBUG
                JsonTokenType originalTokenType;
                int originalDepth;
                long originalBytesConsumed;

                // Todo: validate objects and arrays as well (need to preserve state due to buffering)
                if (ClassType == ClassType.Value)
                {
                    originalTokenType = reader.TokenType;
                    originalDepth = reader.CurrentDepth;
                    originalBytesConsumed = reader.BytesConsumed;
                }
                else
                {
                    originalTokenType = default;
                    originalDepth = default;
                    originalBytesConsumed = default;
                }
#endif

                success = OnTryReadAsObject(ref reader, typeToConvert, options, ref state, ref value);
#if DEBUG
                if (success && ClassType == ClassType.Value)
                {
                    VerifyRead(originalTokenType, originalDepth, originalBytesConsumed, ref reader);
                }
#endif
            }
            else
            {
                JsonTokenType originalTokenType;
                int originalDepth;
                long originalBytesConsumed;

                // Todo: validate objects and arrays as well (need to preserve state due to buffering)
                if (ClassType == ClassType.Value)
                {
                    originalTokenType = reader.TokenType;
                    originalDepth = reader.CurrentDepth;
                    originalBytesConsumed = reader.BytesConsumed;
                }
                else
                {
                    originalTokenType = default;
                    originalDepth = default;
                    originalBytesConsumed = default;
                }

                success = OnTryReadAsObject(ref reader, typeToConvert, options, ref state, ref value);

                if (success && ClassType == ClassType.Value)
                {
                    VerifyRead(originalTokenType, originalDepth, originalBytesConsumed, ref reader);
                }
            }

            if (ClassType != ClassType.Value)
            {
                //state.Current.ReturnValue = value;
                state.Pop(success);
            }

            return success;
        }

        internal bool TryWriteAsObject(Utf8JsonWriter writer, object value, JsonSerializerOptions options, ref WriteStack state)
        {
            // If surpassed flush threshold then return false which will flush stream.
            if (state.FlushThreshold > 0 && writer.BytesPending > state.FlushThreshold)
            {
                return false;
            }

            if (ClassType != ClassType.Value)
            {
                if (writer.CurrentDepth >= options.EffectiveMaxDepth)
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializerCycleDetected(options.MaxDepth);
                }

                Push(ref state, value);
            }

            bool success;

            // For performance on release build, don't verify converter correctness for internal converters.
            if (IsInternalConverter)
            {
#if DEBUG
                int originalDepth = writer.CurrentDepth;
#endif

                success = OnTryWriteAsObject(writer, value, options, ref state);

#if DEBUG
                if (success)
                {
                    VerifyWrite(originalDepth, writer);
                }
#endif
            }
            else
            {
                int originalDepth = writer.CurrentDepth;
                success = OnTryWriteAsObject(writer, value, options, ref state);
                if (success)
                {
                    VerifyWrite(originalDepth, writer);
                }
            }

            if (ClassType != ClassType.Value)
            {
                state.Current.CurrentValue = value;
                state.Pop(success);
            }

            return success;
        }

        internal virtual bool OnTryReadAsObject(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options, ref ReadStack state, ref object value)
        {
            return true;
        }

        internal virtual bool OnTryWriteAsObject(Utf8JsonWriter writer, object value, JsonSerializerOptions options, ref WriteStack state)
        {
            return true;
        }

        internal abstract void VerifyRead(JsonTokenType tokenType, int depth, long bytesConsumed, ref Utf8JsonReader reader);

        internal void VerifyWrite(int originalDepth, Utf8JsonWriter writer)
        {
            if (originalDepth != writer.CurrentDepth)
            {
                ThrowHelper.ThrowJsonException_SerializationConverterWrite(this);
            }
        }
    }
}
