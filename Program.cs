using System;
using System.Threading.Tasks;

namespace DiscordReactionBot
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            DotEnv.Load();

            var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("Environment variable DISCORD_TOKEN is not set. Exiting.");
                return;
            }

            var bot = new Bot(token);
            await bot.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }
    }
}
