namespace DiscordCleaner.Core.Helpers;

public static class DiscordSnowflake
{
    private const long DiscordEpochMs = 1_420_070_400_000L;

    public static string DateToSnowflake(DateTime date)
    {
        long unixMs = ((DateTimeOffset)date).ToUnixTimeMilliseconds();
        long snowflake = (unixMs - DiscordEpochMs) << 22;
        return snowflake.ToString();
    }

    public static DateTime SnowflakeToDate(string snowflakeId)
    {
        if (!ulong.TryParse(snowflakeId, out ulong snowflake))
            return DateTime.MinValue;

        long timestampMs = (long)(snowflake >> 22) + DiscordEpochMs;
        return DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime;
    }

}