// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace System.Text.Json.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        /// <summary>
        /// Demonstrates simple <see cref="TimeSpan"/> converter than only converts to Ticks.
        /// </summary>
        private class TimeSpanConverter : JsonConverter<TimeSpan>
        {
            public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return new TimeSpan(reader.GetInt64());
            }

            public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
            {
                writer.WriteNumberValue(value.Ticks);
            }
        }

        [Fact]
        public static void TimeSpanTicks()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new TimeSpanConverter());

            string json;
            TimeSpan ts;
            TimeSpan tsRoundTripped;

            ts = new TimeSpan(100);
            json = JsonSerializer.Serialize(new TimeSpan(100), options);
            Assert.Equal("100", json);
            tsRoundTripped = JsonSerializer.Deserialize<TimeSpan>(json, options);
            Assert.Equal(ts, tsRoundTripped);

            ts = new TimeSpan(hours: 1, minutes: 2, seconds: 3);
            json = JsonSerializer.Serialize(ts, options);
            Assert.Equal("37230000000", json);
            tsRoundTripped = JsonSerializer.Deserialize<TimeSpan>(json, options);
            Assert.Equal(ts, tsRoundTripped);
        }
    }
}
