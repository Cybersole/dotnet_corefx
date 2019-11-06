// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading;

namespace System.Text.Json
{
    internal partial class JsonClassInfo
    {
        private JsonPropertyInfo AddProperty(Type propertyType, PropertyInfo propertyInfo, Type parentClassType, JsonSerializerOptions options)
        {
            bool hasIgnoreAttribute = (JsonPropertyInfo.GetAttribute<JsonIgnoreAttribute>(propertyInfo) != null);
            if (hasIgnoreAttribute)
            {
                return JsonPropertyInfo.CreateIgnoredPropertyPlaceholder(propertyInfo, options);
            }

            JsonConverter converter = null;
            ClassType classType = GetClassType(
                propertyType,
                parentClassType,
                propertyInfo,
                out Type runtimeType,
                out Type elementType,
                out _,
                out converter,
                checkForAddMethod: false,
                options);

            return CreateProperty(
                declaredPropertyType: propertyType,
                runtimePropertyType: runtimeType,
                propertyInfo,
                parentClassType,
                collectionElementType: elementType,
                converter,
                classType,
                options);
        }

        [PreserveDependency(".ctor()", "System.Text.Json.JsonPropertyInfoNullable`2")]
        [PreserveDependency(".ctor()", "System.Text.Json.Serialization.JsonPropertyInfoNotNullableContravariant`4")]
        internal static JsonPropertyInfo CreateProperty(
            Type declaredPropertyType,
            Type runtimePropertyType,
            PropertyInfo propertyInfo,
            Type parentClassType,
            Type collectionElementType,
            JsonConverter converter,
            ClassType classType,
            JsonSerializerOptions options)
        {
            // Obtain the type of the JsonPropertyInfo class to construct.
            Type propertyInfoClassType;

            Type typeToConvert = converter?.TypeToConvert;
            if (typeToConvert == null)
            {
                typeToConvert = declaredPropertyType;
            }

            // For the covariant case, create JsonPropertyInfoNotNullable. The generic constraints are "where TConverter : TDeclaredProperty".
            if (runtimePropertyType.IsAssignableFrom(typeToConvert))
            {
                propertyInfoClassType = typeof(JsonPropertyInfoNotNullable<,,,>).MakeGenericType(
                    parentClassType,
                    declaredPropertyType,
                    runtimePropertyType,
                    typeToConvert);
            }
            else
            {
                Debug.Assert(typeToConvert.IsAssignableFrom(runtimePropertyType));

                // For the contravariant case, create JsonPropertyInfoNotNullableContravariant. The generic constraints are "where TDeclaredProperty : TConverter".
                propertyInfoClassType = typeof(JsonPropertyInfoNotNullableContravariant<,,,>).MakeGenericType(
                    parentClassType,
                    declaredPropertyType,
                    runtimePropertyType,
                    typeToConvert);
            }

            // Create the JsonPropertyInfo instance.
            JsonPropertyInfo jsonPropertyInfo = (JsonPropertyInfo)Activator.CreateInstance(
                propertyInfoClassType,
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: null,
                culture: null);

            jsonPropertyInfo.Initialize(
                parentClassType,
                declaredPropertyType,
                runtimePropertyType,
                runtimeClassType: classType,
                propertyInfo,
                collectionElementType,
                converter,
                options);

            return jsonPropertyInfo;
        }

        /// <summary>
        /// Create a <see cref="JsonPropertyInfo"/> for a given Type.
        /// A policy property is not a real property on a type; instead it leverages the existing converter
        /// logic and generic support to avoid boxing. It is used with values types, elements from collections and
        /// dictionaries, and collections themselves. Typically it would represent a CLR type such as System.String.
        /// </summary>
        internal static JsonPropertyInfo CreatePolicyProperty(
            Type declaredPropertyType,
            Type runtimePropertyType,
            Type elementType,
            JsonConverter converter,
            ClassType classType,
            JsonSerializerOptions options)
        {
            return CreateProperty(
                declaredPropertyType: declaredPropertyType,
                runtimePropertyType: runtimePropertyType,
                propertyInfo: null, // Not a real property so this is null.
                parentClassType: typeof(object), // a dummy value (not used)
                collectionElementType : elementType,
                converter : converter,
                classType : classType,
                options);
        }

        /// <summary>
        /// Create a <see cref="JsonPropertyInfo"/> for a given Type.
        /// </summary>
        internal JsonPropertyInfo CreateRootProperty(JsonSerializerOptions options)
        {
            JsonConverter converter = options.DetermineConverter(Type, Type, propertyInfo: null);

            return CreateProperty(
                declaredPropertyType: Type,
                runtimePropertyType: Type,
                propertyInfo: null,
                parentClassType: typeof(object), // a dummy value (not used)
                ElementType,
                converter,
                ClassType,
                options);
        }

        internal JsonPropertyInfo GetOrAddPolymorphicProperty(JsonPropertyInfo property, Type declardPropertyType, Type runtimePropertyType)
        {
            JsonPropertyInfo CreateRuntimeProperty(
                (JsonPropertyInfo property, Type runtimePropertyType) key,
                (Type declaredPropertyType, Type classType) arg)
            {
                ClassType classType = GetClassType(
                    key.runtimePropertyType,
                    arg.classType,
                    key.property.PropertyInfo,
                    out _,
                    out Type elementType,
                    out _,
                    out JsonConverter converter,
                    checkForAddMethod: false,
                    Options);

                JsonPropertyInfo runtimeProperty = CreateProperty(
                    declardPropertyType,
                    key.runtimePropertyType,
                    key.property.PropertyInfo,
                    parentClassType: arg.classType,
                    collectionElementType: elementType,
                    converter,
                    classType,
                    options: Options);

                property.CopyRuntimeSettingsTo(runtimeProperty);

                return runtimeProperty;
            }

            ConcurrentDictionary<(JsonPropertyInfo, Type), JsonPropertyInfo> cache =
                LazyInitializer.EnsureInitialized(ref RuntimePropertyCache, () => new ConcurrentDictionary<(JsonPropertyInfo, Type), JsonPropertyInfo>());
#if BUILDING_INBOX_LIBRARY
            return cache.GetOrAdd((property, runtimePropertyType), (key, arg) => CreateRuntimeProperty(key, arg), (declardPropertyType, Type));
#else
            return cache.GetOrAdd((property, runtimePropertyType), key => CreateRuntimeProperty(key, (declardPropertyType, Type)));
#endif
        }
    }
}
