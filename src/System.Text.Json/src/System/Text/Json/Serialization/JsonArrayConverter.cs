// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    }
}
