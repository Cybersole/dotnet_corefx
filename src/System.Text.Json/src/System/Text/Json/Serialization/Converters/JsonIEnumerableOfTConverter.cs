// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace System.Text.Json.Serialization.Converters
{
    internal sealed class JsonIEnumerableOfTConverter<TCollection, TElement> : JsonIEnumerableDefaultConverter<TCollection, TElement> where TCollection : IEnumerable<TElement>
    {
        protected override void CreateCollection(ref ReadStack state)
        {
            Type collectionType = state.Current.JsonClassInfo.Type;
            if (collectionType != RuntimeType && collectionType != TypeToConvert)
            {
                // A collection was specified that just implements IEnumerable<T>; there's not a way to populate that.
                ThrowHelper.ThrowNotSupportedException_SerializationNotSupportedCollection(collectionType);
            }

            state.Current.ReturnValue = new List<TElement>();
        }

        // Consider overriding ConvertCollection to convert the list to an array since a List is mutable.
        //  However, converting from the temporary list to an array is expensive.

        protected override void Add(TElement value, ref ReadStack state)
        {
            ((List<TElement>)state.Current.ReturnValue).Add(value);
        }

        protected override bool OnWriteResume(Utf8JsonWriter writer, TCollection value, JsonSerializerOptions options, ref WriteStack state)
        {
            JsonConverter<TElement> converter = GetElementConverter(ref state);

            IEnumerator<TElement> enumerator;
            if (state.Current.CollectionEnumerator == null)
            {
                enumerator = value.GetEnumerator();
            }
            else
            {
                enumerator = (IEnumerator<TElement>)state.Current.CollectionEnumerator;
            }

            while (enumerator.MoveNext())
            {
                TElement element = enumerator.Current;
                if (!converter.TryWrite(writer, element, options, ref state))
                {
                    state.Current.CollectionEnumerator = enumerator;
                    return false;
                }
            }

            return true;
        }

        internal override Type RuntimeType => typeof(List<TElement>);
    }
}
