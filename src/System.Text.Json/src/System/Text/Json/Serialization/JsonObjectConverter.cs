// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System.Text.Json.Serialization
{
    internal abstract class JsonObjectConverter<T> : JsonResumableConverter<T>
    {
        internal override ClassType ClassType
        {
            get
            {
                return ClassType.Object;
            }
        }

        internal override sealed Type ElementType => null;

        internal override sealed void VerifyRead(JsonTokenType tokenType, int depth, long bytesConsumed, ref Utf8JsonReader reader)
        {
            Debug.Assert(tokenType == JsonTokenType.StartObject);

            if (reader.TokenType != JsonTokenType.EndObject)
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
