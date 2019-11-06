using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace System.Text.Json
{
    internal abstract class JsonPropertyInfo<TRuntimeProperty> : JsonPropertyInfo
    {
        public JsonConverter<TRuntimeProperty> RuntimeConverter
        {
            get
            {
                return (JsonConverter<TRuntimeProperty>)ConverterBase;
            }
        }
    }
}
