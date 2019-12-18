// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json.Serialization.Converters;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// Converts an object or value to or from JSON.
    /// </summary>
    /// <typeparam name="T">The <see cref="Type"/> to convert.</typeparam>
    public abstract class JsonConverter<T> : JsonConverter
    {
        private Type _typeToConvert = typeof(T);

        /// <summary>
        /// When overidden, constructs a new <see cref="JsonConverter{T}"/> instance.
        /// </summary>
        protected internal JsonConverter()
        {
            IsInternalConverter = (GetType().Assembly == typeof(JsonConverter).Assembly);
        }

        /// <summary>
        /// Determines whether the type can be converted.
        /// </summary>
        /// <remarks>
        /// The default implementation is to return True when <paramref name="typeToConvert"/> equals typeof(T).
        /// </remarks>
        /// <param name="typeToConvert"></param>
        /// <returns>True if the type can be converted, False otherwise.</returns>
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert == typeof(T);
        }

        internal override ClassType ClassType
        {
            get
            {
                return ClassType.Value;
            }
        }

        internal override Type ElementType => null;

        // The non-generic API is sealed as it just forwards to the generic version.
        internal override sealed bool OnTryWriteAsObject(Utf8JsonWriter writer, object value, JsonSerializerOptions options, ref WriteStack state)
        {
            T valueOfT = (T)value;
            return OnTryWrite(writer, valueOfT, options, ref state);
        }

        // Provide a default implementation for value converters.
        internal virtual bool OnTryWrite(Utf8JsonWriter writer, T value, JsonSerializerOptions options, ref WriteStack state)
        {
            Write(writer, value, options);
            return true;
        }

        // The non-generic API is sealed as it just forwards to the generic version.
        internal override sealed bool OnTryReadAsObject(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options, ref ReadStack state, ref object value)
        {
            T valueOfT = default;
            bool success = OnTryRead(ref reader, typeToConvert, options, ref state, ref valueOfT);
            if (success)
            {
                value = valueOfT;
            }

            return success;
        }

        // Provide a default implementation for value converters.
        internal virtual bool OnTryRead(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options, ref ReadStack state, ref T value)
        {
            value = Read(ref reader, typeToConvert, options);
            return true;
        }

        /// <summary>
        /// Read and convert the JSON to T.
        /// </summary>
        /// <remarks>
        /// A converter may throw any Exception, but should throw <cref>JsonException</cref> when the JSON is invalid.
        /// </remarks>
        /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
        /// <param name="typeToConvert">The <see cref="Type"/> being converted.</param>
        /// <param name="options">The <see cref="JsonSerializerOptions"/> being used.</param>
        /// <returns>The value that was converted.</returns>
        public abstract T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options);

        // todo: make public pending API review
        internal bool TryRead(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options, ref ReadStack state, ref T value)
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

                success = OnTryRead(ref reader, typeToConvert, options, ref state, ref value);
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

                success = OnTryRead(ref reader, typeToConvert, options, ref state, ref value);

                if (success && ClassType == ClassType.Value)
                {
                    VerifyRead(originalTokenType, originalDepth, originalBytesConsumed, ref reader);
                }
            }

            if (ClassType != ClassType.Value)
            {
                state.Pop(success);
            }

            return success;
        }

        // todo: make public pending API review
        internal bool TryWrite(Utf8JsonWriter writer, T value, JsonSerializerOptions options, ref WriteStack state)
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

                success = OnTryWrite(writer, value, options, ref state);

#if DEBUG
                // Todo: validate objects and arrays as well (need to preserve state due to buffering)
                if (ClassType == ClassType.Value && success)
                {
                    VerifyWrite(originalDepth, writer);
                }
#endif
            }
            else
            {
                int originalDepth = writer.CurrentDepth;

                success = OnTryWrite(writer, value, options, ref state);

                // Todo: validate objects and arrays as well (need to preserve state due to buffering)
                if (ClassType == ClassType.Value && success)
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

        internal bool TryWriteDataExtensionProperty(Utf8JsonWriter writer, T value, JsonSerializerOptions options, ref WriteStack state)
        {
            // If surpassed flush threshold then return false which will flush stream.
            if (state.FlushThreshold > 0 && writer.BytesPending > state.FlushThreshold)
            {
                return false;
            }

            if (writer.CurrentDepth >= options.EffectiveMaxDepth)
            {
                ThrowHelper.ThrowInvalidOperationException_SerializerCycleDetected(options.MaxDepth);
            }

            if (ClassType != ClassType.Value)
            {
                Push(ref state, value);
            }

            // Ignore the naming policy for extension data.
            state.Current.IgnoreDictionaryKeyPolicy = true;

            bool success;
            Debug.Assert(this is JsonDictionaryConverter<T>);
            JsonDictionaryConverter<T> dictionaryConverter = (JsonDictionaryConverter<T>)this;

            // For performance on release build, don't verify converter correctness for internal converters.
            if (IsInternalConverter)
            {
#if DEBUG
                int originalDepth = writer.CurrentDepth;
#endif

                success = dictionaryConverter.OnWriteResume(writer, value, options, ref state);

#if DEBUG
                // Todo: validate objects and arrays as well (need to preserve state due to buffering)
                if (ClassType == ClassType.Value && success)
                {
                    VerifyWrite(originalDepth, writer);
                }
#endif
            }
            else
            {
                int originalDepth = writer.CurrentDepth;
                success = dictionaryConverter.OnWriteResume(writer, value, options, ref state);

                // Todo: validate objects and arrays as well (need to preserve state due to buffering)
                if (ClassType == ClassType.Value && success)
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

        internal override sealed Type TypeToConvert => _typeToConvert;

        internal override void VerifyRead(JsonTokenType tokenType, int depth, long bytesConsumed, ref Utf8JsonReader reader)
        {
            switch (tokenType)
            {
                case JsonTokenType.StartArray:
                    if (reader.TokenType != JsonTokenType.EndArray)
                    {
                        ThrowHelper.ThrowJsonException_SerializationConverterRead(this);
                    }
                    else if (depth != reader.CurrentDepth)
                    {
                        ThrowHelper.ThrowJsonException_SerializationConverterRead(this);
                    }

                    // Should not be possible to have not read anything.
                    Debug.Assert(bytesConsumed < reader.BytesConsumed);
                    break;

                case JsonTokenType.StartObject:
                    if (reader.TokenType != JsonTokenType.EndObject)
                    {
                        ThrowHelper.ThrowJsonException_SerializationConverterRead(this);
                    }
                    else if (depth != reader.CurrentDepth)
                    {
                        ThrowHelper.ThrowJsonException_SerializationConverterRead(this);
                    }

                    // Should not be possible to have not read anything.
                    Debug.Assert(bytesConsumed < reader.BytesConsumed);
                    break;

                default:
                    // Reading a single property value.
                    if (reader.BytesConsumed != bytesConsumed)
                    {
                        ThrowHelper.ThrowJsonException_SerializationConverterRead(this);
                    }

                    // Should not be possible to change token type.
                    Debug.Assert(reader.TokenType == tokenType);

                    break;
            }
        }

        /// <summary>
        /// Write the value as JSON.
        /// </summary>
        /// <remarks>
        /// A converter may throw any Exception, but should throw <cref>JsonException</cref> when the JSON
        /// cannot be created.
        /// </remarks>
        /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write to.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">The <see cref="JsonSerializerOptions"/> being used.</param>
        public abstract void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options);
    }
}
