using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuraWallpaper.Core.Models
{
    public class CustomPropertyItem
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "slider"; // slider, checkbox, select, dropdown, color, textbox, button

        [JsonPropertyName("value")]
        public object? Value { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("min")]
        public double Min { get; set; } = 0;

        [JsonPropertyName("max")]
        public double Max { get; set; } = 100;

        [JsonPropertyName("step")]
        public double Step { get; set; } = 1;

        [JsonPropertyName("options")]
        public List<string>? Options { get; set; }

        [JsonPropertyName("help")]
        public string? Help { get; set; }

        // Helper getters
        [JsonIgnore]
        public double NumericValue
        {
            get
            {
                if (Value is JsonElement elem)
                {
                    if (elem.ValueKind == JsonValueKind.Number) return elem.GetDouble();
                    if (double.TryParse(elem.GetString(), out double d)) return d;
                }
                if (Value is double dVal) return dVal;
                if (Value is int iVal) return iVal;
                if (Value is long lVal) return lVal;
                if (Value != null && double.TryParse(Value.ToString(), out double parsed)) return parsed;
                return Min;
            }
            set => Value = value;
        }

        [JsonIgnore]
        public bool BooleanValue
        {
            get
            {
                if (Value is JsonElement elem)
                {
                    if (elem.ValueKind == JsonValueKind.True) return true;
                    if (elem.ValueKind == JsonValueKind.False) return false;
                    if (bool.TryParse(elem.GetString(), out bool b)) return b;
                }
                if (Value is bool bVal) return bVal;
                if (Value != null && bool.TryParse(Value.ToString(), out bool parsed)) return parsed;
                return false;
            }
            set => Value = value;
        }

        [JsonIgnore]
        public string StringValue
        {
            get
            {
                if (Value is JsonElement elem)
                {
                    return elem.GetString() ?? elem.ToString();
                }
                return Value?.ToString() ?? string.Empty;
            }
            set => Value = value;
        }
    }
}
