using DiscordCleaner.Core.Models;
using DiscordCleaner.Core.Services;

namespace DiscordCleaner.Cli;

class Program
{
    static async Task Main()
    {
        string envPath = Path.Combine(AppContext.BaseDirectory, ".env");
        DotNetEnv.Env.Load(envPath);

        var config = new DiscordConfiguration
        {
            Token = Environment.GetEnvironmentVariable("DISCORD_TOKEN") ?? "",
            UserId = Environment.GetEnvironmentVariable("DISCORD_USER_ID") ?? ""
        };

        if (string.IsNullOrWhiteSpace(config.Token) || string.IsNullOrWhiteSpace(config.UserId))
        {
            Console.WriteLine("ERROR: Token or UserId haven't loaded properly!");
            return;
        }

        using var httpClient = new HttpClient();
        var service = new DiscordCleanerService(config, httpClient);

        service.OnLog += (message, color) =>
        {
            if (color.HasValue)
            {
                Console.ForegroundColor = color.Value;
                Console.WriteLine(message);
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine(message);
            }
        };

        await service.RunAsync();
    }
}