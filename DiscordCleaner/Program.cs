using System.Net;
using System.Text.Json;

namespace DiscordCleaner;

/// <summary>
/// Deletes messages containing links or attachments from all DM channels,
/// starting from a configured date. Uses the user's own account token.
/// Note: automating a user account violates Discord ToS (section 13).
/// </summary>
class DiscordCleaner
{
    /// <summary>Only messages sent on or after this date will be deleted.</summary>
    private static readonly DateTime StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly HttpClient Http = new HttpClient();
    private static readonly Random Rng = new Random();

    /// <summary>Your Discord user token (not a bot token).</summary>
    private static string Token = "";
    /// <summary>Your own Discord user ID — only messages authored by you are deleted.</summary>
    private static string MyUserId = "";

    static async Task Main()
    {
        DotNetEnv.Env.Load();
        Token = Environment.GetEnvironmentVariable("DISCORD_TOKEN") ?? "";
        MyUserId = Environment.GetEnvironmentVariable("DISCORD_USER_ID") ?? "";

        Http.DefaultRequestHeaders.Add("Authorization", Token);

        string afterId = DateToSnowflake(StartDate);
        Console.WriteLine($"Deleting your messages sent after {StartDate:yyyy-MM-dd} (snowflake: {afterId})");

        List<string> channels = await FetchDmChannelIdsAsync();
        Console.WriteLine($"Found {channels.Count} DM channel(s).");

        int totalDeleted = 0;
        int requestsSinceBreak = 0;

        foreach (string channelId in channels)
        {
            Console.WriteLine($"\nScanning channel {channelId}...");

            // Pagination cursor: we advance it after each page.
            string pageAfterId = afterId;

            while (true)
            {
                List<(string id, bool hasMedia)> messages = await FetchMessagesPageAsync(channelId, pageAfterId);

                if (messages.Count == 0)
                    break; // No more messages in this channel.

                // Advance the cursor to the last message ID on this page.
                pageAfterId = messages[^1].id;

                foreach (var (msgId, hasMedia) in messages)
                {
                    if (!hasMedia)
                        continue; // Skip messages without links or attachments.

                    bool deleted = await DeleteWithRetryAsync(channelId, msgId);
                    if (deleted)
                    {
                        totalDeleted++;
                        requestsSinceBreak++;
                        Console.WriteLine($"  Deleted {msgId} (total: {totalDeleted})");
                    }

                    // 1. Zmiana: Podstawowy rozrzut między wiadomościami (1200ms - 3800ms)
                    await RandomDelayAsync(1000, 3500);

                    // 2. Zmiana: Długa przerwa techniczna z dokładnością do milisekund
                    if (requestsSinceBreak >= Rng.Next(20, 40))
                    {
                        // Losujemy czas od 1 minuty (60 000 ms) do 4 minut (240 000 ms)
                        int breakMs = Rng.Next(30_000, 240_001);
                        TimeSpan t = TimeSpan.FromMilliseconds(breakMs);

                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"  [ANTY-BOT] Pausing for {t.Minutes}m {t.Seconds}s {t.Milliseconds}ms...");
                        Console.ResetColor();

                        await Task.Delay(breakMs);
                        requestsSinceBreak = 0;
                    }
                }

                // If we got fewer than 100 messages, this was the last page.
                if (messages.Count < 100)
                    break;
            }

            await RandomDelayAsync(5000, 10000);
        }

        Console.WriteLine($"\nDone. Total messages deleted: {totalDeleted}");
    }

    /// <summary>
    /// Fetches one page (up to 100) of messages from a channel after the given snowflake ID.
    /// Returns a list of (messageId, hasMediaOrLink) tuples for messages authored by the current user.
    /// </summary>
    private static async Task<List<(string id, bool hasMedia)>> FetchMessagesPageAsync(string channelId, string afterId)
    {
        var result = new List<(string, bool)>();

        string url = $"https://discord.com/api/v9/channels/{channelId}/messages?limit=100&after={afterId}";
        HttpResponseMessage response = await Http.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"  Failed to fetch messages: {response.StatusCode}");
            return result;
        }

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        foreach (JsonElement msg in doc.RootElement.EnumerateArray())
        {
            string authorId = msg.GetProperty("author").GetProperty("id").GetString() ?? "";
            if (authorId != MyUserId)
                continue; // Only consider our own messages.

            string msgId = msg.GetProperty("id").GetString() ?? "";

            bool hasAttachments = msg.GetProperty("attachments").GetArrayLength() > 0;
            bool hasEmbeds = msg.GetProperty("embeds").GetArrayLength() > 0;
            bool hasMedia = hasAttachments || hasEmbeds;

            result.Add((msgId, hasMedia));
        }

        return result;
    }

    /// <summary>
    /// Attempts to delete a message, retrying once on a 429 rate-limit response.
    /// Returns true if the message was deleted successfully.
    /// </summary>
    private static async Task<bool> DeleteWithRetryAsync(string channelId, string messageId)
    {
        string url = $"https://discord.com/api/v9/channels/{channelId}/messages/{messageId}";

        for (int attempt = 0; attempt < 2; attempt++)
        {
            HttpResponseMessage response = await Http.DeleteAsync(url);

            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent)
                return true;

            if ((int)response.StatusCode == 429)
            {
                int waitMs = 15_000;
                try
                {
                    string body = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("retry_after", out JsonElement ra))
                        waitMs = (int)(ra.GetDouble() * 1000) + 500;
                }
                catch { }

                Console.WriteLine($"  Rate limited. Waiting {waitMs / 1000.0:F1}s...");
                await Task.Delay(waitMs);
                continue;
            }

            Console.WriteLine($"  Delete failed for {messageId}: {response.StatusCode}");
            return false;
        }

        return false;
    }

    /// <summary>
    /// Fetches all DM (type 1) and group DM (type 3) channel IDs for the current user.
    /// </summary>
    private static async Task<List<string>> FetchDmChannelIdsAsync()
    {
        var ids = new List<string>();

        HttpResponseMessage response = await Http.GetAsync("https://discord.com/api/v9/users/@me/channels");
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to fetch DM channels: {response.StatusCode}");
            return ids;
        }

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        foreach (JsonElement channel in doc.RootElement.EnumerateArray())
        {
            int type = channel.GetProperty("type").GetInt32();
            if (type == 1 || type == 3)
                ids.Add(channel.GetProperty("id").GetString() ?? "");
        }

        return ids;
    }

    /// <summary>
    /// Converts a UTC DateTime to a Discord Snowflake ID representing that moment.
    /// </summary>
    private static string DateToSnowflake(DateTime date)
    {
        const long discordEpochMs = 1_420_070_400_000L;
        long unixMs = ((DateTimeOffset)date).ToUnixTimeMilliseconds();
        long snowflake = (unixMs - discordEpochMs) << 22;
        return snowflake.ToString();
    }

    /// <summary>Waits for a random duration between <paramref name="minMs"/> and <paramref name="maxMs"/> milliseconds.</summary>
    private static Task RandomDelayAsync(int minMs, int maxMs)
        => Task.Delay(Rng.Next(minMs, maxMs + 1));
}