// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.Json
{
    [DebuggerDisplay("Path:{PropertyPath()} Current: ClassType.{Current.JsonClassInfo.ClassType}, {Current.JsonClassInfo.Type.Name}")]
    internal struct WriteStack
    {
        // A field is used instead of a property to avoid value semantics.
        public WriteStackFrame Current;

        public int FlushThreshold;

        private List<WriteStackFrame> _previous;
        private int _index;
        private int _continuationCount;
        private bool _processPushPop;

        public WriteStackFrame Top
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
                    _previous = new List<WriteStackFrame>();
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
                    Current.JsonElementPropertyInfo = classInfo.ElementClassInfo?.PolicyProperty;
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

        // Return a property path as a simple JSONPath using dot-notation when possible. When special characters are present, bracket-notation is used:
        // $.x.y.z
        // $['PropertyName.With.Special.Chars']
        public string PropertyPath()
        {
            StringBuilder sb = new StringBuilder("$");

            for (int i = 0; i < _index; i++)
            {
                AppendStackFrame(sb, _previous[i]);
            }

            AppendStackFrame(sb, Current);
            return sb.ToString();
        }

        private void AppendStackFrame(StringBuilder sb, in WriteStackFrame frame)
        {
            // Append the property name.
            string propertyName = frame.JsonPropertyInfo?.PropertyInfo?.Name;
            AppendPropertyName(sb, propertyName);
        }

        private void AppendPropertyName(StringBuilder sb, string propertyName)
        {
            if (propertyName != null)
            {
                if (propertyName.IndexOfAny(ReadStack.SpecialCharacters) != -1)
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
    }
}
