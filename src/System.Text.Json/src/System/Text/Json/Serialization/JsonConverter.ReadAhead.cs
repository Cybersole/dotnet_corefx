// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.Json.Serialization
{
    /// <summary>
    /// Converts an object or value to or from JSON.
    /// </summary>
    public abstract partial class JsonConverter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PrepareForReadAhead(ref Utf8JsonReader reader, ref ReadStack state)
        {
            if (state.ReadAhead && ClassType == ClassType.Value)
            {
                // When we're reading ahead we always have to save the state
                // as we don't know if the next token is an opening object or
                // array brace.
                state.InitialReaderState = reader.CurrentState;
                state.InitialReaderBytesConsumed = reader.BytesConsumed;
            }
        }

        internal bool SingleValueReadWithReadAhead(ref Utf8JsonReader reader, ref ReadStack state)
        {
            PrepareForReadAhead(ref reader, ref state);

            if (!reader.Read())
            {
                return false;
            }

            return TryReadAhead(ref reader, ref state);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryReadAhead(ref Utf8JsonReader reader, ref ReadStack state)
        {
            if (state.ReadAhead && ClassType == ClassType.Value)
            {
                JsonTokenType tokenType = reader.TokenType;
                if (tokenType == JsonTokenType.StartObject || tokenType == JsonTokenType.StartArray)
                {
                    // Attempt to skip to make sure we have all the data we need.
                    bool complete = reader.TrySkip();

                    // We need to restore the state in all cases as we need to be positioned back before
                    // the current token to either attempt to skip again or to actually read the value in
                    // HandleValue below.

                    reader = new Utf8JsonReader(reader.OriginalSpan.Slice(checked((int)state.InitialReaderBytesConsumed)),
                        isFinalBlock: reader.IsFinalBlock,
                        state: state.InitialReaderState);

                    Debug.Assert(reader.BytesConsumed == 0);
                    state.BytesConsumed += state.InitialReaderBytesConsumed;

                    if (!complete)
                    {
                        // Couldn't read to the end of the object, exit out to get more data in the buffer.
                        return false;
                    }

                    // Success, requeue the reader to the token for HandleValue.
                    reader.Read();
                    Debug.Assert(tokenType == reader.TokenType);
                }
            }

            return true;
        }
    }
}
