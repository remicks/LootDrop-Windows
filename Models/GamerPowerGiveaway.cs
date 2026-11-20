using System.Text.Json.Serialization;

namespace LootDrop.Models;

internal sealed class GamerPowerGiveaway
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("worth")]
    public string? Worth { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("open_giveaway_url")]
    public string? OpenGiveawayUrl { get; set; }

    [JsonPropertyName("published_date")]
    public string? PublishedDate { get; set; }

    [JsonPropertyName("platforms")]
    public string? Platforms { get; set; }

    [JsonPropertyName("end_date")]
    public string? EndDate { get; set; }

    [JsonPropertyName("users")]
    public int Users { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("gamerpower_url")]
    public string? GamerPowerUrl { get; set; }
}
