// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    }
}
