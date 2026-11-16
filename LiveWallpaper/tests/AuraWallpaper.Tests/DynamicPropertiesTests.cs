using System.Collections.Generic;
using System.Text.Json;
using AuraWallpaper.Core.Models;
using Xunit;

namespace AuraWallpaper.Tests
{
    public class DynamicPropertiesTests
    {
        [Fact]
        public void CustomProperties_ParsesLivelyPropertiesSchema()
        {
            string json = @"{
  ""speed"": {
    ""type"": ""slider"",
    ""value"": 25,
    ""min"": 1,
    ""max"": 100,
    ""step"": 1,
    ""text"": ""Animation Speed""
  },
  ""enableStars"": {
    ""type"": ""checkbox"",
    ""value"": true,
    ""text"": ""Enable Background Stars""
  },
  ""themeColor"": {
    ""type"": ""color"",
    ""value"": ""#ff007f"",
    ""text"": ""Primary Theme Color""
  },
  ""preset"": {
    ""type"": ""dropdown"",
    ""value"": ""Neon"",
    ""options"": [""Neon"", ""Retro"", ""Cyberpunk""],
    ""text"": ""Color Palette Preset""
  }
}";

            var properties = JsonSerializer.Deserialize<Dictionary<string, CustomPropertyItem>>(json);
            Assert.NotNull(properties);
            Assert.Equal(4, properties.Count);

            // Slider
            var speed = properties["speed"];
            Assert.Equal("slider", speed.Type);
            Assert.Equal(25, speed.NumericValue);
            Assert.Equal(1, speed.Min);
            Assert.Equal(100, speed.Max);

            // Checkbox
            var stars = properties["enableStars"];
            Assert.Equal("checkbox", stars.Type);
            Assert.True(stars.BooleanValue);

            // Color
            var color = properties["themeColor"];
            Assert.Equal("color", color.Type);
            Assert.Equal("#ff007f", color.StringValue);

            // Dropdown
            var preset = properties["preset"];
            Assert.Equal("dropdown", preset.Type);
            Assert.Equal("Neon", preset.StringValue);
            Assert.NotNull(preset.Options);
            Assert.Equal(3, preset.Options.Count);
        }
    }
}
