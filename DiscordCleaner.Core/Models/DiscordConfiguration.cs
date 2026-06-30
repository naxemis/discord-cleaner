namespace DiscordCleaner.Core.Models;

public class DiscordConfiguration
{
    public string Token { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public DateTime StartDate { get; init; } = DateTime.MinValue;

    public List<string> WhitelistedDomains { get; init; } =
    [
        "youtube.com",
        "youtu.be",
        "spotify.com",
        "github.com",
        "twitch.tv",
        "x.com",
        "discord.com",
        "discord.gg",
        "tiktok.com"
    ];
}