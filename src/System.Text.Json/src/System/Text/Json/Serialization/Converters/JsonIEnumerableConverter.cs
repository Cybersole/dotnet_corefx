// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;

namespace System.Text.Json.Serialization.Converters
{
    internal sealed class JsonIEnumerableConverter : JsonIEnumerableDefaultConverter<IEnumerable, object>
    {
        protected override void CreateCollection(ref ReadStack state)
        {
            state.Current.ReturnValue = new List<object>();
        }

        protected override void ConvertCollection(ref ReadStack state)
        {
            // Use Array instead of a List since a List would be writeable.
            state.Current.ReturnValue = JsonArrayConverter.CreateFromList(ref state, (IList)state.Current.ReturnValue);
        }

        protected override void Add(object value, ref ReadStack state)
        {
            ((List<object>)state.Current.ReturnValue).Add(value);
        }

        protected override bool OnWriteResume(Utf8JsonWriter writer, IEnumerable value, JsonSerializerOptions options, ref WriteStack state)
        {
            JsonConverter<object> converter = GetElementConverter(ref state);

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
                object element = enumerator.Current;
                //JsonConverter converter = options.GetElementConverter(element?.GetType(), ref state);

                if (!converter.TryWriteAsObject(writer, element, options, ref state))
                {
                    state.Current.CollectionEnumerator = enumerator;
                    return false;
                }
            }

            return true;
        }

        internal override Type RuntimeType => typeof(List<object>);
    }
}
