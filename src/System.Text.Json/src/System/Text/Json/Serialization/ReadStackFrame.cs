// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.Json
{
    [DebuggerDisplay("ClassType.{JsonClassInfo.ClassType}, {JsonClassInfo.Type.Name}")]
    internal struct ReadStackFrame
    {
        // new state (todo):
        public bool ProcessedStartToken;
        public bool ProcessedEndToken;
        public bool ProcessedReadName;
        public bool ProcessedName;
        public bool ProcessedReadValue;
        public bool ProcessedValue;

        public bool UseExtensionProperty;

        /// <summary>
        /// User state used to maintain re-entry to support Streaming scenarios.
        /// </summary>
        public object State;

        // The object (POCO or IEnumerable) that is being populated
        public object ReturnValue;
        public JsonClassInfo JsonClassInfo;

        // Support Dictionary keys.
        public string KeyName;

        // Support JSON Path on exceptions.
        public byte[] JsonPropertyName;

        // Current property values.
        public JsonPropertyInfo JsonPropertyInfo;

        // For performance, we order the properties by the first deserialize and PropertyIndex helps find the right slot quicker.
        public int PropertyIndex;
        public List<PropertyRef> PropertyRefCache;

        /// <summary>
        /// Is the current object an Enumerable or Dictionary.
        /// </summary>
        public bool IsProcessingCollection()
        {
            return IsProcessingObject(ClassType.Enumerable | ClassType.Dictionary);
        }

        /// <summary>
        /// Is the current object or property a Dictionary.
        /// </summary>
        public bool IsProcessingDictionary()
        {
            return IsProcessingObject(ClassType.Dictionary);
        }

        /// <summary>
        /// Is the current object or property an Enumerable.
        /// </summary>
        public bool IsProcessingEnumerable()
        {
            return IsProcessingObject(ClassType.Enumerable);
        }

        /// <summary>
        /// Is the current object of the provided <paramref name="classTypes"/>.
        /// </summary>
        public bool IsProcessingObject(ClassType classTypes)
        {
            return (JsonClassInfo.ClassType & classTypes) != 0;
        }

        public void InitializeRoot(Type type, JsonSerializerOptions options)
        {
            JsonClassInfo = options.GetOrAddClass(type);

            if (JsonClassInfo.ClassType == ClassType.Invalid)
            {
                ThrowHelper.ThrowNotSupportedException_SerializationNotSupportedCollection(type);
            }

            // For ClassType.Object, the initial JsonPropertyInfo will be used to obtain the converter for the object.
            JsonPropertyInfo = JsonClassInfo.PolicyProperty;
        }

        public void Reset()
        {
            JsonClassInfo = null;
            PropertyRefCache = null;
            ReturnValue = null;
            EndObject();
        }

        public void EndObject()
        {
            PropertyIndex = 0;
            ProcessedStartToken = false;
            ProcessedEndToken = false;
            State = null;
            EndProperty();
        }

        public void EndProperty()
        {
            UseExtensionProperty = false;
            JsonPropertyInfo = null;
            ProcessedName = false;
            ProcessedReadName = false;
            ProcessedReadValue = false;
            ProcessedValue = false;

            // Don't clear JsonPropertyName as it is used in Path for exception cases.
        }

        public void EndElement()
        {
            ProcessedName = false;
            ProcessedReadName = false;
            ProcessedReadValue = false;
            ProcessedValue = false;
            // Don't clear KeyName as it is used in Path for exception cases.
        }

        public Type GetElementType()
        {
            if (IsProcessingCollection())
            {
                return JsonClassInfo.ElementClassInfo.Type;
            }

            return JsonPropertyInfo.RuntimePropertyType;
        }

        public bool SkipProperty => JsonPropertyInfo.ShouldDeserialize == false;
    }
}
