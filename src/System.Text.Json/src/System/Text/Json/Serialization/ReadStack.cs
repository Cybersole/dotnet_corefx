// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.Json
{
    [DebuggerDisplay("Path:{JsonPath()} Current: ClassType.{Current.JsonClassInfo.ClassType}, {Current.JsonClassInfo.Type.Name}")]
    internal struct ReadStack
    {
        internal static readonly char[] SpecialCharacters = { '.', ' ', '\'', '/', '"', '[', ']', '(', ')', '\t', '\n', '\r', '\f', '\b', '\\', '\u0085', '\u2028', '\u2029' };

        // A field is used instead of a property to avoid value semantics.
        public ReadStackFrame Current;

        private List<ReadStackFrame> _previous;

        /// <summary>
        /// The number of stack frames including Current. _previous will contain _count-1 higher frames.
        /// </summary>
        private int _count;

        /// <summary>
        /// The number of stack frames when the continuation started.
        /// </summary>
        private int _continuationCount;

        // Support the read-ahead feature.
        public JsonReaderState InitialReaderState;
        public long InitialReaderBytesConsumed;

        private void AddCurrent()
        {
            if (_previous == null)
            {
                _previous = new List<ReadStackFrame>();
            }

            if (_count > _previous.Count)
            {
                // Need to allocate a new array element.
                _previous.Add(Current);
            }
            else
            {
                // Use a previously allocated slot.
                _previous[_count - 1] = Current;
            }

            _count++;
        }

        public bool IsContinuation => _continuationCount != 0;

        public void Push(JsonPropertyInfo jsonPropertyInfo)
        {
            if (_continuationCount == 0)
            {
                if (_count == 0)
                {
                    // The first stack frame is held in Current.
                    _count = 1;
                }
                else
                {
                    // Standard push.
                    AddCurrent();

                    Current.Reset();
                    JsonClassInfo classInfo = jsonPropertyInfo.RuntimeClassInfo;
                    Current.JsonClassInfo = classInfo;
                    Current.JsonPropertyInfo = classInfo.PolicyProperty;
                }
            }
            else if (_continuationCount == 1)
            {
                // No need for a push since there is only one stack frame.
                Debug.Assert(_count == 1);
                _continuationCount = 0;
            }
            else
            {
                // A continuation, adjust the index.
                Current = _previous[_count - 1];

                // Check if we are done.
                if (_count == _continuationCount)
                {
                    _continuationCount = 0;
                }
                else
                {
                    _count++;
                }
            }
        }

        public void Pop(bool success)
        {
            Debug.Assert(_count > 0);

            if (!success)
            {
                // Check if we need to initialize the continuation.
                if (_continuationCount == 0)
                {
                    if (_count == 1)
                    {
                        // No need for a continuation since there is only one stack frame.
                        _continuationCount = 1;
                        _count = 1;
                    }
                    else
                    {
                        AddCurrent();
                        _count--;
                        _continuationCount = _count;
                        _count--;
                        Current = _previous[_count - 1];
                    }

                    return;
                }

                if (_continuationCount == 1)
                {
                    // No need for a pop since there is only one stack frame.
                    Debug.Assert(_count == 1);
                    return;
                }

                Debug.Assert(_count > 0);
            }
            else
            {
                Debug.Assert(_continuationCount == 0);
            }

            if (_count > 1)
            {
                Current = _previous[--_count -1];
            }
        }

        public bool IsLastFrame => _count == 0;

        // Return a JSONPath using simple dot-notation when possible. When special characters are present, bracket-notation is used:
        // $.x.y[0].z
        // $['PropertyName.With.Special.Chars']
        public string JsonPath()
        {
            StringBuilder sb = new StringBuilder("$");

            for (int i = 0; i < _count - 1; i++)
            {
                AppendStackFrame(sb, _previous[i]);
            }

            AppendStackFrame(sb, Current);
            return sb.ToString();
        }

        private void AppendStackFrame(StringBuilder sb, in ReadStackFrame frame)
        {
            // Append the property name.
            string propertyName = GetPropertyName(frame);
            AppendPropertyName(sb, propertyName);

            if (frame.JsonClassInfo != null)
            {
                if (frame.IsProcessingDictionary())
                {
                    // For dictionaries add the key.
                    AppendPropertyName(sb, frame.KeyName);
                }
                else if (frame.IsProcessingEnumerable())
                {
                    IEnumerable enumerable = (IEnumerable)frame.ReturnValue;
                    if (enumerable != null)
                    {
                        sb.Append(@"[");
                        sb.Append(GetCount(enumerable));
                        sb.Append(@"]");
                    }
                }
            }
        }

        private static int GetCount(IEnumerable enumerable)
        {
            if (enumerable is ICollection collection)
            {
                return collection.Count;
            }

            int count = 0;
            IEnumerator enumerator = enumerable.GetEnumerator();
            while (enumerator.MoveNext())
            {
                count++;
            }

            return count;
        }

        private void AppendPropertyName(StringBuilder sb, string propertyName)
        {
            if (propertyName != null)
            {
                if (propertyName.IndexOfAny(SpecialCharacters) != -1)
                {
                    sb.Append(@"['");
                    sb.Append(propertyName);
                    sb.Append(@"']");
                }
                else
                {
                    sb.Append('.');
                    sb.Append(propertyName);
                }
            }
        }

        private string GetPropertyName(in ReadStackFrame frame)
        {
            // Attempt to get the JSON property name from the frame.
            byte[] utf8PropertyName = frame.JsonPropertyName;
            if (utf8PropertyName == null)
            {
                // Attempt to get the JSON property name from the JsonPropertyInfo.
                utf8PropertyName = frame.JsonPropertyInfo?.JsonPropertyName;
            }

            string propertyName;
            if (utf8PropertyName != null)
            {
                propertyName = JsonHelpers.Utf8GetString(utf8PropertyName);
            }
            else
            {
                propertyName = null;
            }

            return propertyName;
        }

        /// <summary>
        /// Bytes consumed in the current loop
        /// </summary>
        public long BytesConsumed;

        /// <summary>
        /// Internal flag to let us know that we need to read ahead in the inner read loop.
        /// </summary>
        internal bool ReadAhead;

        /// <summary>
        /// Internal flag to let us know that we need to read ahead in the inner read loop.
        /// </summary>
        internal bool SupportContinuation;
    }
}
