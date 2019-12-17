// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System.Text.Json.Serialization.Converters
{
    internal abstract class JsonArrayConverter<TCollection, TElement> : JsonResumableConverter<TCollection>
    {
        private Type _elementType = typeof(TElement);

        internal override ClassType ClassType
        {
            get
            {
                return ClassType.Enumerable;
            }
        }

        internal override Type ElementType => _elementType;

        internal override sealed void VerifyRead(JsonTokenType tokenType, int depth, long bytesConsumed, ref Utf8JsonReader reader)
        {
            Debug.Assert(tokenType == JsonTokenType.StartArray);

            if (reader.TokenType != JsonTokenType.EndArray)
            {
                ThrowHelper.ThrowJsonException_SerializationConverterRead(this);
            }
            else if (depth != reader.CurrentDepth)
            {
                ThrowHelper.ThrowJsonException_SerializationConverterRead(this);
            }
        }
    }
}
