// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace System.Text.Json.Serialization.Converters
{
    internal class JsonIEnumerableConverterFactory : JsonConverterFactory
    {
        private static readonly JsonIDictionaryConverter s_IDictionaryConverter = new JsonIDictionaryConverter();
        private static readonly JsonIEnumerableConverter s_IEnumerableConverter = new JsonIEnumerableConverter();
        private static readonly JsonIListConverter s_IListConverter = new JsonIListConverter();

        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(IEnumerable).IsAssignableFrom(typeToConvert);
        }

        [PreserveDependency(".ctor", "System.Text.Json.Serialization.Converters.JsonArrayConverter`2")]
        [PreserveDependency(".ctor", "System.Text.Json.Serialization.Converters.JsonEnumerableConverter`2")]
        [PreserveDependency(".ctor", "System.Text.Json.Serialization.Converters.JsonIDictionaryOfStringTValueConverter`2")]
        [PreserveDependency(".ctor", "System.Text.Json.Serialization.Converters.JsonDictionaryOfStringTValueConverter`2")]
        [PreserveDependency(".ctor", "System.Text.Json.Serialization.Converters.JsonIEnumerableOfTConverter`2")]
        [PreserveDependency(".ctor", "System.Text.Json.Serialization.Converters.JsonICollectionOfTConverter`2")]
        [PreserveDependency(".ctor", "System.Text.Json.Serialization.Converters.JsonIListOfTConverter`2")]
        [PreserveDependency(".ctor", "System.Text.Json.Serialization.Converters.JsonListOfTConverter`2")]
        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            JsonConverter converter = null;
            Type converterType = null;
            Type elementType = null;
            Type actualTypeToConvert;

            // Array
            if (typeToConvert.IsArray)
            {
                // Verify that we don't have a multidimensional array.
                if (typeToConvert.GetArrayRank() > 1)
                {
                    return null;
                }

                converterType = typeof(JsonEnumerableConverter<,>);
                elementType = typeToConvert.GetElementType();
            }
            // List<>
            else if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericBaseClass(typeof(List<>))) != null)
            {
                converterType = typeof(JsonListOfTConverter<,>);
                elementType = actualTypeToConvert.GetGenericArguments()[0];
            }
            // Dictionary<string,>
            else if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericBaseClass(typeof(Dictionary<,>))) != null)
            {
                if (actualTypeToConvert.GetGenericArguments()[0] == typeof(string))
                {
                    converterType = typeof(JsonDictionaryOfStringTValueConverter<,>);
                    elementType = actualTypeToConvert.GetGenericArguments()[1];
                }
                else
                {
                    return null;
                }
            }
            // IDictionary<string,>
            else if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericInterface(typeof(IDictionary<,>))) != null)
            {
                if (actualTypeToConvert.GetGenericArguments()[0] == typeof(string))
                {
                    converterType = typeof(JsonIDictionaryOfStringTValueConverter<,>);
                    elementType = actualTypeToConvert.GetGenericArguments()[1];
                }
                else
                {
                    return null;
                }
            }
            // IList<>
            else if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericInterface(typeof(IList<>))) != null)
            {
                converterType = typeof(JsonIListOfTConverter<,>);
                elementType = actualTypeToConvert.GetGenericArguments()[0];
            }
            // ICollection<>
            else if ((actualTypeToConvert = typeToConvert.GetCompatibleGenericInterface(typeof(ICollection<>))) != null)
            {
                converterType = typeof(JsonICollectionOfTConverter<,>);
                elementType = actualTypeToConvert.GetGenericArguments()[0];
            }
            // IEnumerable<>
            else if (typeToConvert.IsInterface &&
                (actualTypeToConvert = typeToConvert.GetCompatibleGenericInterface(typeof(IEnumerable<>))) != null)
            {
                converterType = typeof(JsonIEnumerableOfTConverter<,>);
                elementType = actualTypeToConvert.GetGenericArguments()[0];
            }
            // Check for non-generics after checking for generics.
            // IDictionary
            else if (typeof(IDictionary).IsAssignableFrom(typeToConvert))
            {
                return s_IDictionaryConverter;
            }
            else if (typeof(IList).IsAssignableFrom(typeToConvert))
            {
                return s_IListConverter;
            }
            else if (typeof(IEnumerable).IsAssignableFrom(typeToConvert))
            {
                return s_IEnumerableConverter;
            }

            if (converterType != null)
            {
                Type genericType = converterType.MakeGenericType(typeToConvert, elementType);

                converter = (JsonConverter)Activator.CreateInstance(
                    genericType,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    binder: null,
                    args: null,
                    culture: null);
            }

            return converter;
        }
    }
}
