// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Serialization;

namespace System.Text.Json
{
    [DebuggerDisplay("PropertyInfo={PropertyInfo}, Element={ElementClassInfo}")]
    internal abstract class JsonPropertyInfo
    {
        public static readonly JsonPropertyInfo s_missingProperty = GetPropertyPlaceholder();
        public static readonly JsonPropertyInfo s_notSupportedProperty = GetPropertyPlaceholder();

        private JsonClassInfo _elementClassInfo;
        private JsonClassInfo _runtimeClassInfo;
        private JsonClassInfo _declaredTypeClassInfo;

        public bool CanBeNull { get; private set; }

        public ClassType ClassType;

        public abstract JsonConverter ConverterBase { get; set; }

        private static JsonPropertyInfo GetPropertyPlaceholder()
        {
            JsonPropertyInfo info = new JsonPropertyInfoNotNullable<object, object, object, object>();
            info.IsPropertyPolicy = false;
            info.ShouldDeserialize = false;
            info.ShouldSerialize = false;
            return info;
        }

        // Copy any settings defined at run-time to the new property.
        public void CopyRuntimeSettingsTo(JsonPropertyInfo other)
        {
            other.EscapedName = EscapedName;
            other.HasGetter = HasGetter;
            other.HasSetter = HasSetter;
            other.IgnoreNullValues = IgnoreNullValues;
            other.Name = Name;
            other.NameAsString = NameAsString;
            other.PropertyNameKey = PropertyNameKey;
            other.ShouldDeserialize = ShouldDeserialize;
            other.ShouldSerialize = ShouldSerialize;
        }

        public abstract IList CreateConverterList();

        public abstract IDictionary CreateConverterDictionary();

        public abstract IEnumerable CreateImmutableCollectionInstance(ref ReadStack state, Type collectionType, string delegateKey, IList sourceList, JsonSerializerOptions options);

        public abstract IDictionary CreateImmutableDictionaryInstance(ref ReadStack state, Type collectionType, string delegateKey, IDictionary sourceDictionary, JsonSerializerOptions options);

        // Create a property that is ignored at run-time. It uses the same type (typeof(sbyte)) to help
        // prevent issues with unsupported types and helps ensure we don't accidently (de)serialize it.
        public static JsonPropertyInfo CreateIgnoredPropertyPlaceholder(PropertyInfo propertyInfo, JsonSerializerOptions options)
        {
            JsonPropertyInfo jsonPropertyInfo = new JsonPropertyInfoNotNullable<sbyte, sbyte, sbyte, sbyte>();
            jsonPropertyInfo.Options = options;
            jsonPropertyInfo.PropertyInfo = propertyInfo;
            jsonPropertyInfo.DeterminePropertyName();

            Debug.Assert(!jsonPropertyInfo.ShouldDeserialize);
            Debug.Assert(!jsonPropertyInfo.ShouldSerialize);

            return jsonPropertyInfo;
        }

        public Type DeclaredPropertyType { get; private set; }

        private void DeterminePropertyName()
        {
            if (PropertyInfo == null)
            {
                return;
            }

            JsonPropertyNameAttribute nameAttribute = GetAttribute<JsonPropertyNameAttribute>(PropertyInfo);
            if (nameAttribute != null)
            {
                string name = nameAttribute.Name;
                if (name == null)
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializerPropertyNameNull(ParentClassType, this);
                }

                NameAsString = name;
            }
            else if (Options.PropertyNamingPolicy != null)
            {
                string name = Options.PropertyNamingPolicy.ConvertName(PropertyInfo.Name);
                if (name == null)
                {
                    ThrowHelper.ThrowInvalidOperationException_SerializerPropertyNameNull(ParentClassType, this);
                }

                NameAsString = name;
            }
            else
            {
                NameAsString = PropertyInfo.Name;
            }

            Debug.Assert(NameAsString != null);

            // At this point propertyName is valid UTF16, so just call the simple UTF16->UTF8 encoder.
            Name = Encoding.UTF8.GetBytes(NameAsString);

            // Cache the escaped property name.
            EscapedName = JsonEncodedText.Encode(Name, Options.Encoder);

            ulong key = JsonClassInfo.GetKey(Name);
            PropertyNameKey = key;
        }

        private void DetermineSerializationCapabilities()
        {
            if ((ClassType & (ClassType.Enumerable | ClassType.Dictionary)) == 0)
            {
                // We serialize if there is a getter + not ignoring readonly properties.
                ShouldSerialize = HasGetter && (HasSetter || !Options.IgnoreReadOnlyProperties);

                // We deserialize if there is a setter.
                ShouldDeserialize = HasSetter;
            }
            else
            {
                if (HasGetter)
                {
                    if (ConverterBase == null)
                    {
                        ThrowCollectionNotSupportedException();
                    }

                    ShouldSerialize = true;

                    if (HasSetter)
                    {
                        ShouldDeserialize = true;
                    }
                }
            }
        }

        /// <summary>
        /// Return the JsonClassInfo for the element type, or null if the property is not an enumerable or dictionary.
        /// </summary>
        /// <remarks>
        /// This should not be called during warm-up (initial creation of JsonClassInfos) to avoid recursive behavior
        /// which could result in a StackOverflowException.
        /// </remarks>
        public JsonClassInfo ElementClassInfo
        {
            get
            {
                if (_elementClassInfo == null && ElementType != null)
                {
                    Debug.Assert(ClassType == ClassType.Enumerable || ClassType == ClassType.Dictionary);

                    _elementClassInfo = Options.GetOrAddClass(ElementType);
                }

                return _elementClassInfo;
            }
        }

        public Type ElementType { get; set; }

        // The escaped name passed to the writer.
        // Use a field here (not a property) to avoid value semantics.
        public JsonEncodedText? EscapedName;

        public static TAttribute GetAttribute<TAttribute>(PropertyInfo propertyInfo) where TAttribute : Attribute
        {
            return (TAttribute)propertyInfo?.GetCustomAttribute(typeof(TAttribute), inherit: false);
        }

        public virtual void GetPolicies()
        {
            DetermineSerializationCapabilities();
            DeterminePropertyName();
            IgnoreNullValues = Options.IgnoreNullValues;
        }

        public abstract object GetValueAsObject(object obj);

        public bool HasGetter { get; set; }
        public bool HasSetter { get; set; }

        public virtual void Initialize(
            Type parentClassType,
            Type declaredPropertyType,
            Type runtimePropertyType,
            ClassType runtimeClassType,
            PropertyInfo propertyInfo,
            Type elementType,
            JsonConverter converter,
            JsonSerializerOptions options)
        {
            ParentClassType = parentClassType;
            DeclaredPropertyType = declaredPropertyType;
            RuntimePropertyType = runtimePropertyType;
            ClassType = runtimeClassType;
            PropertyInfo = propertyInfo;
            ElementType = elementType;
            Options = options;

            bool treatAsNullable = Nullable.GetUnderlyingType(runtimePropertyType) != null;
            CanBeNull = treatAsNullable || !runtimePropertyType.IsValueType;

            ConverterBase = converter;
        }

        public abstract bool TryCreateEnumerableAddMethod(object target, out object addMethodDelegate);

        public abstract object CreateEnumerableAddMethod(MethodInfo addMethod, object target);

        public bool IgnoreNullValues { get; private set; }

        public bool IsPropertyPolicy { get; protected set; }

        // The name from a Json value. This is cached for performance on first deserialize.
        public byte[] JsonPropertyName { get; set; }

        // The name of the property with any casing policy or the name specified from JsonPropertyNameAttribute.
        public byte[] Name { get; private set; }
        public string NameAsString { get; private set; }

        // Key for fast property name lookup.
        public ulong PropertyNameKey { get; set; }

        // Options can be referenced here since all JsonPropertyInfos originate from a JsonClassInfo that is cached on JsonSerializerOptions.
        protected JsonSerializerOptions Options { get; set; }

        public abstract bool GetMemberAndWriteJson(object obj, ref WriteStack state, Utf8JsonWriter writer);
        public abstract bool GetMemberAndWriteJsonExtensionData(object obj, ref WriteStack state, Utf8JsonWriter writer);
        public abstract bool ReadJsonAndSetMember(object obj, ref ReadStack state, ref Utf8JsonReader reader);

        public bool ReadJsonAndAddExtensionProperty(object obj, ref ReadStack state, ref Utf8JsonReader reader)
        {
            JsonConverter<JsonElement> converter = JsonSerializerOptions.GetJsonElementConverter();
            JsonElement jsonElement = default;
            if (!converter.TryRead(ref reader, typeof(JsonElement), Options, ref state, ref jsonElement))
            {
                // No need to set a partial object here since JsonElement is a struct that must be read in full.
                return false;
            }

            object propValue = GetValueAsObject(obj);
            if (propValue is IDictionary<string, object> dictionaryObject)
            {
                dictionaryObject[state.Current.KeyName] = jsonElement;
            }
            else
            {
                IDictionary<string, JsonElement> dictionaryJsonElement = (IDictionary<string, JsonElement>)propValue;
                dictionaryJsonElement[state.Current.KeyName] = jsonElement;
            }

            return true;
        }

        public Type ParentClassType { get; private set; }

        public PropertyInfo PropertyInfo { get; private set; }

        public JsonClassInfo RuntimeClassInfo
        {
            get
            {
                if (_runtimeClassInfo == null)
                {
                    _runtimeClassInfo = Options.GetOrAddClass(RuntimePropertyType);
                }

                return _runtimeClassInfo;
            }
        }

        public JsonClassInfo DeclaredTypeClassInfo
        {
            get
            {
                if (_declaredTypeClassInfo == null)
                {
                    _declaredTypeClassInfo = Options.GetOrAddClass(DeclaredPropertyType);
                }

                return _declaredTypeClassInfo;
            }
        }

        public Type RuntimePropertyType { get; private set; }

        public abstract void SetValueAsObject(object obj, object value);

        public bool ShouldSerialize { get; private set; }
        public bool ShouldDeserialize { get; private set; }

        public void ThrowCollectionNotSupportedException()
        {
            throw ThrowHelper.GetNotSupportedException_SerializationNotSupportedCollection(RuntimePropertyType, ParentClassType, PropertyInfo);
        }
    }
}
