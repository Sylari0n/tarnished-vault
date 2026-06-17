using System.Text.Json;
using System.Text.Json.Serialization;

namespace TarnishedVault.Api.Models;

public class Build
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    [JsonPropertyName("game")]
    public string Game { get; set; } = string.Empty;

    [JsonPropertyName("section")]
    public string Section { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = "Anonymous";

    [JsonPropertyName("screenshotUrl")]
    public string? ScreenshotUrl { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("data")]
    public Dictionary<string, JsonElement> Data { get; set; } = new();
}
