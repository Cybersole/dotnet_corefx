// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace System.Text.Json.Serialization.Converters
{
    internal sealed class JsonListOfTConverter<TCollection, TElement> : JsonIEnumerableDefaultConverter<TCollection, TElement> where TCollection: List<TElement>, new()
    {
        protected override void CreateCollection(ref ReadStack state)
        {
            state.Current.ReturnValue = new TCollection();
        }

        protected override void Add(TElement value, ref ReadStack state)
        {
            ((TCollection)state.Current.ReturnValue).Add(value);
        }

        protected override bool OnWriteResume(Utf8JsonWriter writer, TCollection value, JsonSerializerOptions options, ref WriteStack state)
        {
            JsonConverter<TElement> elementConverter = GetElementConverter(ref state);

            List<TElement>.Enumerator enumerator;
            if (state.Current.CollectionEnumerator == null)
            {
                enumerator = value.GetEnumerator();
            }
            else
            {
                enumerator = (List<TElement>.Enumerator)state.Current.CollectionEnumerator;
            }

            while (enumerator.MoveNext())
            {
                TElement element = enumerator.Current;
                if (!elementConverter.TryWrite(writer, element, options, ref state))
                {
                    state.Current.CollectionEnumerator = enumerator;
                    return false;
                }
            }

            return true;
        }
    }
}
