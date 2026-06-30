namespace DiscordCleaner.Core.Models;

public class DiscordConfiguration
{
    public string Token { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public DateTime StartDate { get; init; } = DateTime.MinValue;

}