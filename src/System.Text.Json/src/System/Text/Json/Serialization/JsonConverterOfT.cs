// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

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

        // By default allow converters to process null
        internal override bool ConvertNullValue => true;

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
            Debug.Assert(value == null || value is T);

            T valueOfT;
            if (value == null)
            {
                if (TypeToConvert.IsValueType)
                {
                    valueOfT = default;
                }
                else
                {
                    valueOfT = (T)state.Current.ReturnValue;
                }
            }
            else
            {
                valueOfT = (T)value;
            }

            bool success = OnTryRead(ref reader, typeToConvert, options, ref state, ref valueOfT);
            value = valueOfT;
            return success;
        }

        // Provide a default implementation for value converters.
        internal virtual bool OnTryRead(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options, ref ReadStack state, ref T value)
        {
            if (IsInternalConverter)
            {
#if DEBUG
                JsonTokenType originalTokenType = reader.TokenType;
                int originalDepth = reader.CurrentDepth;
                long originalBytesConsumed = reader.BytesConsumed;
#endif

                value = Read(ref reader, typeToConvert, options);
#if DEBUG
                VerifyRead(originalTokenType, originalDepth, originalBytesConsumed, ref reader);
#endif
            }
            else
            {
                JsonTokenType originalTokenType = reader.TokenType;
                int originalDepth = reader.CurrentDepth;
                long originalBytesConsumed = reader.BytesConsumed;

                value = Read(ref reader, typeToConvert, options);

                VerifyRead(originalTokenType, originalDepth, originalBytesConsumed, ref reader);
            }

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
            if (reader.TokenType == JsonTokenType.Null && !ConvertNullValue)
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
                JsonTokenType originalTokenType = reader.TokenType;
                int originalDepth = reader.CurrentDepth;
                long originalBytesConsumed = reader.BytesConsumed;
#endif

                success = OnTryRead(ref reader, typeToConvert, options, ref state, ref value);
#if DEBUG
                VerifyRead(originalTokenType, originalDepth, originalBytesConsumed, ref reader);
#endif
            }
            else
            {
                JsonTokenType originalTokenType = reader.TokenType;
                int originalDepth = reader.CurrentDepth;
                long originalBytesConsumed = reader.BytesConsumed;

                success = OnTryRead(ref reader, typeToConvert, options, ref state, ref value);

                VerifyRead(originalTokenType, originalDepth, originalBytesConsumed, ref reader);
            }

            if (ClassType != ClassType.Value)
            {
                state.Current.ReturnValue = value;
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
                VerifyWrite(originalDepth, writer);
#endif
            }
            else
            {
                int originalDepth = writer.CurrentDepth;
                success = OnTryWrite(writer, value, options, ref state);
                VerifyWrite(originalDepth, writer);
            }

            if (ClassType != ClassType.Value)
            {
                state.Current.CurrentValue = value;
                state.Pop(success);
            }

            return success;
        }

        internal override sealed Type TypeToConvert => _typeToConvert;

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
