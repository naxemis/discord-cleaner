using System.Net;
using System.Text.Json;
using DiscordCleaner.Core.Models;
using DiscordCleaner.Core.Helpers;

namespace DiscordCleaner.Core.Services;

public class DiscordCleanerService
{
    private readonly DiscordConfiguration _config;
    private readonly HttpClient _http;
    private readonly Random _rng = new();

    public event Action<string, ConsoleColor?>? OnLog;

    public DiscordCleanerService(DiscordConfiguration config, HttpClient http)
    {
        _config = config;
        _http = http;

        if (!_http.DefaultRequestHeaders.Contains("Authorization"))
        {
            _http.DefaultRequestHeaders.Add("Authorization", _config.Token);
        }
    }

    public async Task RunAsync()
    {
        Log($"Deleting messages from {_config.StartDate:yyyy-MM-dd} to now...");

        List<string> channels = await FetchDmChannelIdsAsync();
        Log($"Found {channels.Count} DM channel(s).");
        channels.Sort((a, b) => ulong.Parse(b).CompareTo(ulong.Parse(a)));

        int totalDeleted = 0;
        int requestsSinceBreak = 0;

        foreach (string channelId in channels)
        {
            Log($"\nScanning channel {channelId}...");
            string? pageBeforeId = null;

            while (true)
            {
                var (messages, reachedCutoff) = await FetchMessagesPageAsync(channelId, pageBeforeId);

                if (messages.Count == 0) break;
                pageBeforeId = messages[^1].Id;

                foreach (var msg in messages)
                {
                    if (!msg.ToDelete) continue;

                    bool deleted = await DeleteWithRetryAsync(channelId, msg.Id);
                    if (deleted)
                    {
                        totalDeleted++;
                        requestsSinceBreak++;
                        Log($"Deleted {msg.Id} (Total: {totalDeleted})");
                    }

                    await Task.Delay(_rng.Next(1000, 3501));

                    if (requestsSinceBreak >= _rng.Next(20, 40))
                    {
                        int breakMs = _rng.Next(30_000, 240_001);
                        TimeSpan t = TimeSpan.FromMilliseconds(breakMs);
                        Log($"Pausing for {t.Minutes}m {t.Seconds}s {t.Milliseconds}ms...", ConsoleColor.Cyan);

                        await Task.Delay(breakMs);
                        requestsSinceBreak = 0;
                    }
                }

                if (reachedCutoff || messages.Count < 100) break;
            }

            await Task.Delay(_rng.Next(5000, 10001));
        }

        Log($"\nDone. Total messages deleted: {totalDeleted}");
    }

    private async Task<(List<DiscordMessage> Messages, bool ReachedCutoff)> FetchMessagesPageAsync(string channelId, string? beforeId)
    {
        var result = new List<DiscordMessage>();
        string url = $"https://discord.com/api/v9/channels/{channelId}/messages?limit=100{(string.IsNullOrEmpty(beforeId) ? "" : $"&before={beforeId}")}";

        HttpResponseMessage response = await _http.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            Log($"Failed to fetch message: {response.StatusCode}");
            return (result, false);
        }

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        foreach (JsonElement msg in doc.RootElement.EnumerateArray())
        {
            string msgId = msg.GetProperty("id").GetString() ?? "";
            DateTime msgDate = DiscordSnowflake.SnowflakeToDate(msgId);

            if (msgDate < _config.StartDate)
                return (result, true);

            string authorId = msg.GetProperty("author").GetProperty("id").GetString() ?? "";
            if (authorId != _config.UserId)
                continue;

            bool hasAttachments = msg.GetProperty("attachments").GetArrayLength() > 0;
            bool hasEmbeds = msg.GetProperty("embeds").GetArrayLength() > 0;
            string content = msg.GetProperty("content").GetString() ?? "";
            bool hasLink = content.Contains("http://") || content.Contains("https://");

            bool shouldDelete = hasAttachments || hasEmbeds || hasLink;

            result.Add(new DiscordMessage(msgId, shouldDelete));
        }

        return (result, false);
    }

    private async Task<bool> DeleteWithRetryAsync(string channelId, string messageId)
    {
        string url = $"https://discord.com/api/v9/channels/{channelId}/messages/{messageId}";

        for (int attempt = 0; attempt < 2; attempt++)
        {
            HttpResponseMessage response = await _http.DeleteAsync(url);

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

                Log($"ERROR: Rate limit! Waiting {waitMs / 1000.0:F1}s...");
                await Task.Delay(waitMs);
                continue;
            }

            Log($"ERROR: Delete failed for {messageId} ({response.StatusCode})!");
            return false;
        }

        return false;
    }

    private async Task<List<string>> FetchDmChannelIdsAsync()
    {
        var ids = new List<string>();
        HttpResponseMessage response = await _http.GetAsync("https://discord.com/api/v9/users/@me/channels");

        if (!response.IsSuccessStatusCode)
        {
            Log($"ERROR: Failed to fetch DM channels ({response.StatusCode})!");
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

    private void Log(string message, ConsoleColor? color = null)
    {
        OnLog?.Invoke(message, color);
    }
}