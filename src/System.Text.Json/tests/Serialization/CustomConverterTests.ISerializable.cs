// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;
using System.Runtime.Serialization;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.Buffers;

namespace System.Text.Json.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        /// <summary>
        /// Demonstrates custom converter for <see cref="System.Runtime.Serialization.ISerializable"/>.
        /// This converter should be added after all other custom converters.
        /// </summary>
        internal class ConverterForISerializable : JsonConverterFactory
        {
            public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
            {
                JsonConverter converter = (JsonConverter)Activator.CreateInstance(
                    typeof(ConverterForISerializableInner<>).MakeGenericType(type),
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    args: null,
                    culture: CultureInfo.InvariantCulture);

                return converter;
            }

            public override bool CanConvert(Type typeToConvert)
            {
                // Avoid using ISerializable for primitives.
                if (Type.GetTypeCode(typeToConvert) != TypeCode.Object)
                {
                    return false;
                }

                bool canConvert = typeof(ISerializable).IsAssignableFrom(typeToConvert);
                if (!canConvert)
                {
                    return false;
                }

                // SerializableAttribute needs to exist by convention.
                Attribute serializableAttribute = typeToConvert.GetCustomAttribute(typeof(SerializableAttribute));
                if (serializableAttribute == null)
                {
                    return false;
                }

                return true;
            }

            /// <summary>
            /// Converts to a JSON format using an array of <see cref="SerializationInfoItem"/>.
            /// Alternative is to attempt to JSON of the actual primitive type (e.g. DateTime, Decimal, Double)
            /// but since that would not contain type information like <see cref="SerializationInfoItem"/> it is not
            /// possible to distinguish Decimal from Double for example which may result in loss of precission or
            /// overflow exceptions.
            /// </summary>
            /// <typeparam name="T"></typeparam>
            internal class ConverterForISerializableInner<T> : JsonConverter<T>
            {
                public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                {
                    if (reader.TokenType != JsonTokenType.StartArray)
                    {
                        throw new JsonException();
                    }

                    var serializationInfo = new SerializationInfo(typeToConvert, new JsonFormatterConverter(options));

                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndArray)
                        {
                            break;
                        }

                        if (reader.TokenType != JsonTokenType.StartObject)
                        {
                            throw new JsonException();
                        }

                        var item = new SerializationInfoItem();

                        // Name
                        reader.Read();
                        string propertyName = reader.GetString();
                        if (propertyName != nameof(SerializationInfoItem.Name))
                        {
                            throw new JsonException();
                        }

                        reader.Read();
                        item.Name = reader.GetString();

                        // TypeCode
                        reader.Read();
                        propertyName = reader.GetString();
                        if (propertyName != nameof(SerializationInfoItem.TypeCode))
                        {
                            throw new JsonException();
                        }

                        reader.Read();
                        item.TypeCode = (TypeCode)reader.GetInt32();

                        // Value
                        reader.Read();
                        propertyName = reader.GetString();
                        if (propertyName != nameof(SerializationInfoItem.Value))
                        {
                            throw new JsonException();
                        }

                        Type objectType = TypeCodeToType(item.TypeCode);
                        item.Value = JsonSerializer.Deserialize(ref reader, objectType, options);

                        // EndObject
                        reader.Read();
                        if (reader.TokenType != JsonTokenType.EndObject)
                        {
                            throw new JsonException();
                        }

                        AddItem(item, serializationInfo);
                    }

                    T value = (T)Activator.CreateInstance(
                        typeToConvert,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        binder: null,
                        args: new object[] { serializationInfo, new StreamingContext() },
                        culture: CultureInfo.InvariantCulture);

                    return value;
                }

                public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
                {
                    writer.WriteStartArray();

                    var formatterConverter = new FormatterConverter();
                    SerializationInfo serializationInfo = new SerializationInfo(value.GetType(), formatterConverter);
                    ((ISerializable)value).GetObjectData(serializationInfo, new StreamingContext());

                    foreach (SerializationEntry serializationEntry in serializationInfo)
                    {
                        if (serializationEntry.Name != null)
                        {
                            Type objectType = serializationEntry.ObjectType;

                            writer.WriteStartObject();

                            writer.WriteString(nameof(SerializationInfoItem.Name), serializationEntry.Name);

                            writer.WriteNumber(nameof(SerializationInfoItem.TypeCode), (int)Type.GetTypeCode(objectType));

                            writer.WritePropertyName(nameof(SerializationInfoItem.Value));
                            JsonSerializer.Serialize(writer, serializationEntry.Value, objectType, options);

                            writer.WriteEndObject();
                        }
                    }

                    writer.WriteEndArray();
                }

                private class SerializationInfoItem
                {
                    public string Name { get; set; }
                    public TypeCode TypeCode { get; set; }
                    public object Value { get; set; }
                }

                private static Type TypeCodeToType(TypeCode code)
                {
                    switch (code)
                    {
                        case TypeCode.Boolean:
                            return typeof(bool);
                        case TypeCode.Byte:
                            return typeof(byte);
                        case TypeCode.Char:
                            return typeof(char);
                        case TypeCode.DateTime:
                            return typeof(DateTime);
                        case TypeCode.Decimal:
                            return typeof(decimal);
                        case TypeCode.Double:
                            return typeof(double);
                        case TypeCode.Empty:
                            return null;
                        case TypeCode.Int16:
                            return typeof(short);
                        case TypeCode.Int32:
                            return typeof(int);
                        case TypeCode.Int64:
                            return typeof(long);
                        case TypeCode.SByte:
                            return typeof(sbyte);
                        case TypeCode.Single:
                            return typeof(float);
                        case TypeCode.String:
                            return typeof(string);
                        case TypeCode.UInt16:
                            return typeof(ushort);
                        case TypeCode.UInt32:
                            return typeof(uint);
                        case TypeCode.UInt64:
                            return typeof(ulong);
                    }

                    return typeof(object);
                }

                private static void AddItem(SerializationInfoItem item, SerializationInfo info)
                {
                    string name = item.Name;

                    switch (item.TypeCode)
                    {
                        case TypeCode.Boolean:
                            info.AddValue(name, (bool)item.Value);
                            break;
                        case TypeCode.Byte:
                            info.AddValue(name, (byte)item.Value);
                            break;
                        case TypeCode.Char:
                            info.AddValue(name, (char)item.Value);
                            break;
                        case TypeCode.DateTime:
                            info.AddValue(name, (DateTime)item.Value);
                            break;
                        case TypeCode.Decimal:
                            info.AddValue(name, (decimal)item.Value);
                            break;
                        case TypeCode.Double:
                            info.AddValue(name, (double)item.Value);
                            break;
                        case TypeCode.Empty:
                            info.AddValue(name, null);
                            break;
                        case TypeCode.Int16:
                            info.AddValue(name, (short)item.Value);
                            break;
                        case TypeCode.Int32:
                            info.AddValue(name, (int)item.Value);
                            break;
                        case TypeCode.Int64:
                            info.AddValue(name, (long)item.Value);
                            break;
                        case TypeCode.SByte:
                            info.AddValue(name, (sbyte)item.Value);
                            break;
                        case TypeCode.Single:
                            info.AddValue(name, (float)item.Value);
                            break;
                        case TypeCode.String:
                            info.AddValue(name, (string)item.Value);
                            break;
                        case TypeCode.UInt16:
                            info.AddValue(name, (ushort)item.Value);
                            break;
                        case TypeCode.UInt32:
                            info.AddValue(name, (uint)item.Value);
                            break;
                        case TypeCode.UInt64:
                            info.AddValue(name, (ulong)item.Value);
                            break;
                        default:
                            info.AddValue(name, item.Value);
                            break;
                    }
                }

                private class JsonFormatterConverter : IFormatterConverter
                {
                    private readonly JsonSerializerOptions _options;
                    private readonly FormatterConverter _converter = new FormatterConverter();

                    public JsonFormatterConverter(JsonSerializerOptions options)
                    {
                        _options = options;
                    }

                    public object Convert(object value, Type type)
                    {
                        Debug.Assert(value != null);

                        byte[] bytes;

                        if (value is JsonElement jsonElement)
                        {
                            var buffer = new ArrayBufferWriter<byte>();
                            Utf8JsonWriter writer = new Utf8JsonWriter(buffer);
                            jsonElement.WriteTo(writer);
                            writer.Flush();

                            bytes = buffer.WrittenMemory.ToArray();
                        }
                        else if (type.IsEnum)
                        {
                            // Special case for Enum since we only have an integer value and not an Enum type.
                            if (!Enum.TryParse(type, value.ToString(), ignoreCase: true, out object enumValue))
                            {
                                throw new JsonException();
                            }

                            bytes = JsonSerializer.SerializeToUtf8Bytes(enumValue, type, _options);
                        }
                        else
                        {
                            bytes = JsonSerializer.SerializeToUtf8Bytes(value, type, _options);
                        }

                        return JsonSerializer.Deserialize(bytes, type, _options);
                    }

                    public object Convert(object value, TypeCode typeCode)
                    {
                        return System.Convert.ChangeType(value, typeCode, CultureInfo.InvariantCulture);
                    }

                    public bool ToBoolean(object value)
                    {
                        return _converter.ToBoolean(value);
                    }

                    public byte ToByte(object value)
                    {
                        return _converter.ToByte(value);
                    }

                    public char ToChar(object value)
                    {
                        return _converter.ToChar(value);
                    }

                    public DateTime ToDateTime(object value)
                    {
                        return _converter.ToDateTime(value);
                    }

                    public decimal ToDecimal(object value)
                    {
                        return _converter.ToDecimal(value);
                    }

                    public double ToDouble(object value)
                    {
                        return _converter.ToDouble(value);
                    }

                    public short ToInt16(object value)
                    {
                        return _converter.ToInt16(value);
                    }

                    public int ToInt32(object value)
                    {
                        return _converter.ToInt32(value);
                    }

                    public long ToInt64(object value)
                    {
                        return _converter.ToInt64(value);
                    }

                    public sbyte ToSByte(object value)
                    {
                        return _converter.ToSByte(value);
                    }

                    public float ToSingle(object value)
                    {
                        return _converter.ToSingle(value);
                    }

                    public string ToString(object value)
                    {
                        return _converter.ToString(value);
                    }

                    public ushort ToUInt16(object value)
                    {
                        return _converter.ToUInt16(value);
                    }

                    public uint ToUInt32(object value)
                    {
                        return _converter.ToUInt32(value);
                    }

                    public ulong ToUInt64(object value)
                    {
                        return _converter.ToUInt64(value);
                    }
                }
            }
        }

        [Fact]
        public static void TimeZoneInfoISerializable()
        {
            TimeZoneInfo obj = TimeZoneInfo.Local;

            var options = new JsonSerializerOptions();
            options.Converters.Add(new ConverterForISerializable());
            options.Converters.Add(new TimeSpanConverter());

            string json = JsonSerializer.Serialize(obj, options);

            TimeZoneInfo infoRoundTripped = JsonSerializer.Deserialize<TimeZoneInfo>(json, options);
            Assert.Equal(obj.BaseUtcOffset, infoRoundTripped.BaseUtcOffset);
            Assert.Equal(obj.DaylightName, infoRoundTripped.DaylightName);
            Assert.Equal(obj.DisplayName, infoRoundTripped.DisplayName);
            Assert.Equal(obj.Id, infoRoundTripped.Id);
            Assert.Equal(obj.StandardName, infoRoundTripped.StandardName);
            Assert.Equal(obj.SupportsDaylightSavingTime, infoRoundTripped.SupportsDaylightSavingTime);
            Assert.Equal(obj.GetAdjustmentRules().Length, infoRoundTripped.GetAdjustmentRules().Length);

            string jsonRoundTripped = JsonSerializer.Serialize(obj, options);
            Assert.Equal(json, jsonRoundTripped);
        }

        [Fact]
        public static void JsonExceptionISerializable()
        {
            JsonException obj = new JsonException("Message", "Path", 1, 2, new Exception("Hello"));

            var options = new JsonSerializerOptions();
            options.Converters.Add(new ConverterForISerializable());

            string json = JsonSerializer.Serialize(obj, options);

            obj = JsonSerializer.Deserialize<JsonException>(json, options);
            Assert.Equal("Message", obj.Message);
            Assert.Equal("Path", obj.Path);
            Assert.Equal(1, obj.LineNumber);
            Assert.Equal(2, obj.BytePositionInLine);
            Assert.Equal("Hello", obj.InnerException.Message);

            string jsonRoundTripped = JsonSerializer.Serialize(obj, options);
            Assert.Equal(json, jsonRoundTripped);
        }
    }
}
