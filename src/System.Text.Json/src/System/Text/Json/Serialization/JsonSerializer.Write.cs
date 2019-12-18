// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Text.Json
{
    public static partial class JsonSerializer
    {
        // There are three conditions to consider for an object (primitive value, enumerable or object) being processed here:
        // 1) The object type was specified as the root-level return type to a Deserialize method.
        // 2) The object is a property on a parent object.
        // 3) The object is an element in an enumerable.
        private static bool Write(
            Utf8JsonWriter writer,
            JsonSerializerOptions options,
            ref WriteStack state)
        {
            try
            {
                JsonPropertyInfo jsonPropertyInfo = state.Current.JsonClassInfo.PolicyProperty;

                object value = state.Current.CurrentValue;

                if (jsonPropertyInfo.ConverterBase.TryWriteAsObject(writer, value, options, ref state))
                {
                    state.Current.CurrentValue = value;
                    return true;
                }
                else
                {
                    state.Current.CurrentValue = value;
                }
            }
            catch (InvalidOperationException ex) when (ex.Source == ThrowHelper.ExceptionSourceValueToRethrowAsJsonException)
            {
                ThrowHelper.ReThrowWithPath(state, ex);
            }
            catch (JsonException ex)
            {
                ThrowHelper.AddExceptionInformation(state, ex);
                throw;
            }

            return false;
        }
    }
}
