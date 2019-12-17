// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Runtime.CompilerServices;

namespace System.Text.Json
{
    internal struct WriteStackFrame
    {
        // new state (todo):
        public bool ProcessedStartToken;
        public bool ProcessedEndToken;
        public bool ProcessedPropertyName;
        public bool ProcessedPropertyValue;
        public bool IgnoreDictionaryKeyPolicy;

        // The object (POCO or IEnumerable) that is being populated.
        public object CurrentValue;
        public JsonClassInfo JsonClassInfo;
        public JsonPropertyInfo JsonElementPropertyInfo;

        // Support Dictionary keys.
        public string KeyName;

        // The current IEnumerable or IDictionary.
        public IEnumerator CollectionEnumerator;

        // The current property.
        public int PropertyEnumeratorIndex;
        public JsonPropertyInfo JsonPropertyInfo;

        public void InitializeRoot(Type type, JsonSerializerOptions options)
        {
            JsonClassInfo = options.GetOrAddClass(type);

            if (JsonClassInfo.ClassType == ClassType.Invalid)
            {
                ThrowHelper.ThrowNotSupportedException_SerializationNotSupportedCollection(type);
            }

            // For ClassType.Object, the initial JsonPropertyInfo will be used to obtain the converter for the object.
            JsonPropertyInfo = JsonClassInfo.PolicyProperty;

            if ((JsonClassInfo.ClassType & (ClassType.Enumerable | ClassType.Dictionary)) != 0)
            {
                JsonElementPropertyInfo = JsonClassInfo.ElementClassInfo.PolicyProperty;
            }
        }

        public void Reset()
        {
            CurrentValue = null;
            CollectionEnumerator = null;
            IgnoreDictionaryKeyPolicy = false;
            JsonClassInfo = null;
            JsonElementPropertyInfo = null;
            ProcessedStartToken = false;
            ProcessedEndToken = false;
            PropertyEnumeratorIndex = 0;

            EndProperty();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndProperty()
        {
            JsonPropertyInfo = null;
            KeyName = null;
            ProcessedPropertyName = false;
            ProcessedPropertyValue = false;
        }

        public void EndDictionary()
        {
            CollectionEnumerator = null;
        }

        public void EndArray()
        {
            CollectionEnumerator = null;
        }

        // AggressiveInlining used although a large method it is only called from one location and is on a hot path.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NextProperty()
        {
            if (PropertyEnumeratorIndex >= JsonClassInfo.PropertyCacheArray.Length)
            {
                return false;
            }

            EndProperty();
            ++PropertyEnumeratorIndex;
            return true;
        }
    }
}
