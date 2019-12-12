// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Converters;

namespace System.Text.Json
{
    /// <summary>
    /// Provides options to be used with <see cref="JsonSerializer"/>.
    /// </summary>
    public sealed partial class JsonSerializerOptions
    {
        // The global list of built-in simple converters.
        private static readonly Dictionary<Type, JsonConverter> s_defaultSimpleConverters = GetDefaultSimpleConverters();

        // The global list of built-in converters that override CanConvert().
        private static readonly List<JsonConverter> s_defaultFactoryConverters = GetDefaultConverters();

        // The cached converters (custom or built-in).
        private readonly ConcurrentDictionary<Type, JsonConverter> _converters = new ConcurrentDictionary<Type, JsonConverter>();

        private static Dictionary<Type, JsonConverter> GetDefaultSimpleConverters()
        {
            var converters = new Dictionary<Type, JsonConverter>(NumberOfSimpleConverters);

            // Use a dictionary for simple converters.
            foreach (JsonConverter converter in DefaultSimpleConverters)
            {
                converters.Add(converter.TypeToConvert, converter);
            }

            Debug.Assert(NumberOfSimpleConverters == converters.Count);

            return converters;
        }

        // Get the list for converters that implement CanConvert().
        private static List<JsonConverter> GetDefaultConverters()
        {
            const int NumberOfConverters = 5;

            var converters = new List<JsonConverter>(NumberOfConverters);

            // Nullable converter should always be first since it forwards to any nullable type.
            converters.Add(new JsonValueConverterNullableFactory());

            converters.Add(new JsonConverterEnum());
            converters.Add(new JsonKeyValuePairConverter());

            // Enumerable converter should always be next to last since it can convert any IEnumerable.
            converters.Add(new JsonIEnumerableConverterFactory());

            // Object converter should always be last since it can convert any type.
            converters.Add(new JsonObjectFactoryConverter());

            Debug.Assert(NumberOfConverters == converters.Count);

            return converters;
        }

        /// <summary>
        /// The list of custom converters.
        /// </summary>
        /// <remarks>
        /// Once serialization or deserialization occurs, the list cannot be modified.
        /// </remarks>
        public IList<JsonConverter> Converters { get; }

        internal JsonConverter DetermineConverter(Type parentClassType, Type runtimePropertyType, PropertyInfo propertyInfo)
        {
            JsonConverter converter = null;

            // Priority 1: attempt to get converter from JsonConverterAttribute on property.
            if (propertyInfo != null)
            {
                JsonConverterAttribute converterAttribute = (JsonConverterAttribute)
                    GetAttributeThatCanHaveMultiple(parentClassType, typeof(JsonConverterAttribute), propertyInfo);

                if (converterAttribute != null)
                {
                    converter = GetConverterFromAttribute(converterAttribute, typeToConvert: runtimePropertyType, classTypeAttributeIsOn: parentClassType, propertyInfo);
                }
            }

            if (converter == null)
            {
                converter = GetConverter(runtimePropertyType);
            }

            if (converter is JsonConverterFactory factory)
            {
                converter = factory.GetConverterInternal(runtimePropertyType, this);
            }

            return converter;
        }

        /// <summary>
        /// Returns the converter for the specified type.
        /// </summary>
        /// <param name="typeToConvert">The type to return a converter for.</param>
        /// <returns>
        /// The first converter that supports the given type, or null if there is no converter.
        /// </returns>
        public JsonConverter GetConverter(Type typeToConvert)
        {
            if (_converters.TryGetValue(typeToConvert, out JsonConverter converter))
            {
                return converter;
            }

            // Priority 2: Attempt to get custom converter added at runtime.
            // Currently there is not a way at runtime to overide the [JsonConverter] when applied to a property.
            foreach (JsonConverter item in Converters)
            {
                if (item.CanConvert(typeToConvert))
                {
                    converter = item;
                    break;
                }
            }

            // Priority 3: Attempt to get converter from [JsonConverter] on the type being converted.
            if (converter == null)
            {
                JsonConverterAttribute converterAttribute = (JsonConverterAttribute)
                    GetAttributeThatCanHaveMultiple(typeToConvert, typeof(JsonConverterAttribute));

                if (converterAttribute != null)
                {
                    converter = GetConverterFromAttribute(converterAttribute, typeToConvert: typeToConvert, classTypeAttributeIsOn: typeToConvert, propertyInfo: null);
                }
            }

            // Priority 4: Attempt to get built-in converter.
            if (converter == null)
            {
                if (s_defaultSimpleConverters.TryGetValue(typeToConvert, out JsonConverter foundConverter))
                {
                    converter = foundConverter;
                }
                else
                {
                    foreach (JsonConverter item in s_defaultFactoryConverters)
                    {
                        if (item.CanConvert(typeToConvert))
                        {
                            converter = item;
                            break;
                        }
                    }
                }
            }

            // Allow redirection for generic types or the enum converter.
            if (converter is JsonConverterFactory factory)
            {
                converter = factory.GetConverterInternal(typeToConvert, this);
                // Allow null converters from the factory. This will result in a NotSupportedException later.
            }

            if (converter != null)
            {
                Type converterTypeToConvert = converter.TypeToConvert;

                if (!converterTypeToConvert.IsAssignableFrom(typeToConvert) &&
                    !typeToConvert.IsAssignableFrom(converterTypeToConvert))
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializationConverterNotCompatible(converter.GetType(), typeToConvert);
                }
            }

            // Only cache the value once (de)serialization has occurred since new converters can be added that may change the result.
            if (_haveTypesBeenCreated)
            {
                // A null converter is allowed here and cached.

                // Ignore failure case here in multi-threaded cases since the cached item will be equivalent.
                _converters.TryAdd(typeToConvert, converter);
            }

            return converter;
        }

        private JsonConverter GetElementConverter(Type type, ref WriteStack state)
        {
            JsonPropertyInfo collectionPropertyInfo = state.Current.JsonPropertyInfo;
            Debug.Assert((collectionPropertyInfo.ClassType & (ClassType.Dictionary | ClassType.Enumerable)) != 0);

            // Use the current property info it is for the correct type.
            JsonPropertyInfo elementPropertyInfo = state.Current.JsonElementPropertyInfo;

            if (type == null)
            {
                // Use the default converter for the null value.
                type = collectionPropertyInfo.ElementType;
            }

            if (type != typeof(object) && elementPropertyInfo.RuntimePropertyType != type)
            {
                elementPropertyInfo = collectionPropertyInfo.RuntimeClassInfo.GetOrAddPolymorphicProperty(
                    collectionPropertyInfo,
                    elementPropertyInfo.DeclaredPropertyType,
                    type);

                state.Current.JsonElementPropertyInfo = elementPropertyInfo;
            }

            return elementPropertyInfo.ConverterBase;
        }

        private JsonConverter GetPropertyConverter(Type type, ref WriteStack state)
        {
            JsonPropertyInfo propertyInfo = state.Current.JsonPropertyInfo;
            Debug.Assert((propertyInfo.ClassType & (ClassType.Dictionary | ClassType.Enumerable)) == 0);

            if (type == null)
            {
                // Use the default converter for the null value.
                type = propertyInfo.DeclaredTypeClassInfo.Type;
            }

            if (type != typeof(object) && propertyInfo.RuntimePropertyType != type)
            {
                propertyInfo = propertyInfo.RuntimeClassInfo.GetOrAddPolymorphicProperty(
                    propertyInfo,
                    propertyInfo.DeclaredPropertyType,
                    type);

                state.Current.JsonPropertyInfo = propertyInfo;
            }

            return propertyInfo.ConverterBase;
        }

        internal JsonConverter GetConverter(Type type, ref WriteStack state)
        {
            JsonPropertyInfo jsonPropertyInfo = state.Current.JsonPropertyInfo;

            if ((jsonPropertyInfo.ClassType & (ClassType.Dictionary | ClassType.Enumerable)) != 0)
            {
                return GetElementConverter(type, ref state);
            }

            return GetPropertyConverter(type, ref state);
        }

        internal bool HasConverter(Type typeToConvert)
        {
            return GetConverter(typeToConvert) != null;
        }

        private JsonConverter GetConverterFromAttribute(JsonConverterAttribute converterAttribute, Type typeToConvert, Type classTypeAttributeIsOn, PropertyInfo propertyInfo)
        {
            JsonConverter converter;

            Type type = converterAttribute.ConverterType;
            if (type == null)
            {
                // Allow the attribute to create the converter.
                converter = converterAttribute.CreateConverter(typeToConvert);
                if (converter == null)
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializationConverterOnAttributeNotCompatible(classTypeAttributeIsOn, propertyInfo, typeToConvert);
                }
            }
            else
            {
                ConstructorInfo ctor = type.GetConstructor(Type.EmptyTypes);
                if (!typeof(JsonConverter).IsAssignableFrom(type) || !ctor.IsPublic)
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializationConverterOnAttributeInvalid(classTypeAttributeIsOn, propertyInfo);
                }

                converter = (JsonConverter)Activator.CreateInstance(type);
            }

            if (!converter.CanConvert(typeToConvert))
            {
                ThrowHelper.ThrowInvalidOperationException_SerializationConverterOnAttributeNotCompatible(classTypeAttributeIsOn, propertyInfo, typeToConvert);
            }

            return converter;
        }

        private static Attribute GetAttributeThatCanHaveMultiple(Type classType, Type attributeType, PropertyInfo propertyInfo)
        {
            object[] attributes = propertyInfo?.GetCustomAttributes(attributeType, inherit: false);
            return GetAttributeThatCanHaveMultiple(attributeType, classType, propertyInfo, attributes);
        }

        private static Attribute GetAttributeThatCanHaveMultiple(Type classType, Type attributeType)
        {
            object[] attributes = classType.GetCustomAttributes(attributeType, inherit: false);
            return GetAttributeThatCanHaveMultiple(attributeType, classType, null, attributes);
        }

        private static Attribute GetAttributeThatCanHaveMultiple(Type attributeType, Type classType, PropertyInfo propertyInfo, object[] attributes)
        {
            if (attributes.Length == 0)
            {
                return null;
            }

            if (attributes.Length == 1)
            {
                return (Attribute)attributes[0];
            }

            ThrowHelper.ThrowInvalidOperationException_SerializationDuplicateAttribute(attributeType, classType, propertyInfo);
            return default;
        }

        private const int NumberOfSimpleConverters = 21;

        private static IEnumerable<JsonConverter> DefaultSimpleConverters
        {
            get
            {
                // When adding to this, update NumberOfSimpleConverters above.

                yield return new JsonConverterBoolean();
                yield return new JsonConverterByte();
                yield return new JsonConverterByteArray();
                yield return new JsonConverterChar();
                yield return new JsonConverterDateTime();
                yield return new JsonConverterDateTimeOffset();
                yield return new JsonConverterDouble();
                yield return new JsonConverterDecimal();
                yield return new JsonConverterGuid();
                yield return new JsonConverterInt16();
                yield return new JsonConverterInt32();
                yield return new JsonConverterInt64();
                yield return new JsonConverterJsonElement();
                yield return new JsonConverterObject();
                yield return new JsonConverterSByte();
                yield return new JsonConverterSingle();
                yield return new JsonConverterString();
                yield return new JsonConverterUInt16();
                yield return new JsonConverterUInt32();
                yield return new JsonConverterUInt64();
                yield return new JsonConverterUri();
            }
        }
    }
}
