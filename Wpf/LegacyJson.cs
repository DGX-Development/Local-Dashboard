#if NET10_MIGRATION
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows7.0")]
namespace System.Web.Script.Serialization {
    internal sealed class JavaScriptSerializer {
        public int MaxJsonLength { get; set; } = 2097152;
        readonly JsonSerializerOptions options;
        public JavaScriptSerializer() {
            options = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true, MaxDepth = 100 };
            options.Converters.Add(new UntypedValueConverter());
        }
        public string Serialize(object value) {
            string json = JsonSerializer.Serialize(value, options);
            if (json.Length > MaxJsonLength) throw new ArgumentException("JSON exceeds the maximum length.");
            return json;
        }
        public T Deserialize<T>(string json) {
            if (json == null) throw new ArgumentNullException(nameof(json));
            if (json.Length > MaxJsonLength) throw new ArgumentException("JSON exceeds the maximum length.");
            try { return JsonSerializer.Deserialize<T>(json, options); }
            catch (JsonException error) { throw new ArgumentException("Invalid JSON.", error); }
        }
        public object DeserializeObject(string json) { return Deserialize<object>(json); }
        public T ConvertToType<T>(object value) { return Deserialize<T>(Serialize(value)); }
        sealed class UntypedValueConverter : JsonConverter<object> {
            public override object Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) {
                using (var document = JsonDocument.ParseValue(ref reader)) return Convert(document.RootElement);
            }
            static object Convert(JsonElement element) {
                switch (element.ValueKind) {
                    case JsonValueKind.Object: return element.EnumerateObject().ToDictionary(p => p.Name, p => Convert(p.Value));
                    case JsonValueKind.Array: return element.EnumerateArray().Select(Convert).ToArray();
                    case JsonValueKind.String: return element.GetString();
                    case JsonValueKind.Number:
                        int small; if (element.TryGetInt32(out small)) return small;
                        long large; if (element.TryGetInt64(out large)) return large;
                        decimal precise; if (element.TryGetDecimal(out precise)) return precise;
                        return element.GetDouble();
                    case JsonValueKind.True: return true;
                    case JsonValueKind.False: return false;
                    default: return null;
                }
            }
            public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) {
                if (value.GetType() == typeof(object)) { writer.WriteStartObject(); writer.WriteEndObject(); }
                else JsonSerializer.Serialize(writer, value, value.GetType(), options);
            }
        }
    }
}
#endif
