// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Text.Json.Serialization.Converters
{
    // e.g. IDictionary, Hashtable, Dictionary<,> IDictionary<,>, SortedList etc.
    internal abstract class JsonDictionaryConverter<T> : JsonResumableConverter<T>
    {
        internal override ClassType ClassType
        {
            get
            {
                return ClassType.Dictionary;
            }
        }

        protected internal abstract bool OnWriteResume(Utf8JsonWriter writer, T dictionary, JsonSerializerOptions options, ref WriteStack state);
    }
}
