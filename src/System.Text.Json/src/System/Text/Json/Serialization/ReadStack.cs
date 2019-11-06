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

        private List<ReadStackFrame> _previous;
        private int _index;
        private int _continuationCount;
        private bool _processPushPop;

        public JsonReaderState InitialReaderState;
        public long InitialReaderBytesConsumed;

        // A field is used instead of a property to avoid value semantics.
        public ReadStackFrame Current;
        public ReadStackFrame Top
        {
            get
            {
                if (_index > 0)
                {
                    return _previous[0];
                }

                return Current;
            }
        }

        public void Push(JsonPropertyInfo jsonPropertyInfo)
        {
            if (_processPushPop)
            {
                if (_previous == null)
                {
                    _previous = new List<ReadStackFrame>();
                }

                if (_continuationCount > 0)
                {
                    // A continuation, adjust the index.
                    Current = _previous[_index++];
                    _continuationCount--;
                }
                else
                {
                    AddCurrent();

                    Current.Reset();
                    JsonClassInfo classInfo = jsonPropertyInfo.RuntimeClassInfo;
                    Current.JsonClassInfo = classInfo;
                    Current.JsonPropertyInfo = classInfo.PolicyProperty;
                }
            }
            else
            {
                // Ignore first Push(); Current is used without requiring _previous List.
                _processPushPop = true;
            }
        }

        private void AddCurrent()
        {
            if (_index == _previous.Count)
            {
                // Need to allocate a new array element.
                _previous.Add(Current);
            }
            else
            {
                Debug.Assert(_index < _previous.Count);

                // Use a previously allocated slot.
                _previous[_index] = Current;
            }

            _index++;
        }

        public void Pop(bool success)
        {
            Debug.Assert(success || _continuationCount == 0);

            if (!success && _continuationCount == 0)
            {
                AddCurrent();
                _continuationCount--;

                Current = _previous[--_index];

                // Reset JsonPropertyInfo for objects where the initial value is the class.
                Current.JsonPropertyInfo = Current.JsonClassInfo.PolicyProperty;
            }
            else
            {
                if (_index > 0)
                {
                    Current = _previous[--_index];
                }
                else
                {
                    Debug.Assert(_processPushPop);
#if DEBUG
                    // Cause an error if we push once too many times.
                    _processPushPop = false;
#endif
                }
            }
        }

        public bool IsLastFrame => _index == 0;

        // Return a JSONPath using simple dot-notation when possible. When special characters are present, bracket-notation is used:
        // $.x.y[0].z
        // $['PropertyName.With.Special.Chars']
        public string JsonPath()
        {
            StringBuilder sb = new StringBuilder("$");

            for (int i = 0; i < _index; i++)
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

        public void SetToTop()
        {
            Current = Top;
            _index = 0;
        }
    }
}
