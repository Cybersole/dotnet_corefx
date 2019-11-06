using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Encodings.Web;
using Xunit;

namespace System.Text.Json.Serialization.Tests
{
    // Random tests for verifying refactoring
    public static partial class TempTests
    {
        private class MyPOCO
        {
            public string String { get; set; }
            public int Int { get; set; }
            //public IDictionary<string, int> Dictionary { get; set; }
            public IDictionary Dictionary { get; set; }
            public List<MyPOCO2> List { get; set; }
        }

        private class MyPOCO2
        {
            public string String { get; set; }
            public int Int { get; set; }
        }

        [Fact]
        public static void Test1()
        {
            const string Json =
                @"{""String"":""Hello World""" +
                @",""Int"":1" +
                @",""Dictionary"":{""Hello2"":2,""Hello3"":3}" +
                @",""List"":[{""String"":""Hello2"",""Int"":2}]" +
                @"}";

            MyPOCO poco = JsonSerializer.Deserialize<MyPOCO>(Json);

            string json = JsonSerializer.Serialize(poco);
            Assert.Equal(Json, json);
        }
    }
}
