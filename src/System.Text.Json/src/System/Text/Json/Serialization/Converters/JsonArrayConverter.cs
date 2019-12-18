// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;

namespace System.Text.Json.Serialization.Converters
{
    internal sealed class JsonDefaultArrayConverter<TCollection, TElement> : JsonIEnumerableDefaultConverter<TCollection, TElement> where TCollection: IEnumerable
    {
        protected override void CreateCollection(ref ReadStack state)
        {
            state.Current.ReturnValue = new List<TElement>();
        }

        protected override void ConvertCollection(ref ReadStack state)
        {
            List<TElement> list = (List<TElement>)state.Current.ReturnValue;
            state.Current.ReturnValue = list.ToArray();
        }

        protected override void Add(TElement value, ref ReadStack state)
        {
            ((List<TElement>)state.Current.ReturnValue).Add(value);
        }

        protected override bool OnWriteResume(Utf8JsonWriter writer, TCollection value, JsonSerializerOptions options, ref WriteStack state)
        {
            JsonConverter<TElement> converter = GetElementConverter(ref state);

            IEnumerator enumerator;
            if (state.Current.CollectionEnumerator == null)
            {
                enumerator = value.GetEnumerator();
            }
            else
            {
                enumerator = state.Current.CollectionEnumerator;
            }

            while (enumerator.MoveNext())
            {
                TElement element = (TElement)enumerator.Current;
                if (!converter.TryWrite(writer, element, options, ref state))
                {
                    state.Current.CollectionEnumerator = enumerator;
                    return false;
                }
            }

            return true;
        }
    }
}
