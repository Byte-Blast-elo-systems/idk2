using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace DiscordReactionBot
{
    public class Bot
    {
        private readonly DiscordSocketClient _client;
        private readonly string _token;
        private readonly Services.StorageService _storage;
        private readonly Commands.CommandHandler _commands;

        public Bot(string token)
        {
            _token = token;
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.MessageContent | GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions
            };
            _client = new DiscordSocketClient(config);
            _storage = new Services.StorageService("config");
            _commands = new Commands.CommandHandler(_client, _storage);
        }

        public async Task StartAsync()
        {
            _client.Log += LogAsync;
            _client.MessageReceived += MessageReceivedAsync;

            await _storage.LoadAllAsync();

            var token = _token;
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
        }

        private Task LogAsync(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(SocketMessage msg)
        {
            if (msg.Author.IsBot) return;

            // First handle commands if message starts with prefix
            if (msg.Content.StartsWith("?"))
            {
                await _commands.HandleCommandAsync(msg);
                return;
            }

            // Non-command messages: apply reaction rules
            // Blocking check
            if (_storage.Blocked.Contains(msg.Author.Id)) return;

            if (!_storage.Config.Settings.ReactEnabled) return;

            // Check user-specific rule
            var userRule = _storage.Reactions.FirstOrDefault(r => r.Type == Models.ReactionType.User && r.UserId == msg.Author.Id);
            Models.ReactionRule? ruleToApply = null;
            if (userRule != null) ruleToApply = userRule;
            else
            {
                var globalRule = _storage.Reactions.FirstOrDefault(r => r.Type == Models.ReactionType.Global);
                if (globalRule != null) ruleToApply = globalRule;
            }

            if (ruleToApply == null) return;

            foreach (var token in ruleToApply.Emojis)
            {
                var emote = EmoteParser.ParseEmote(token, _client);
                if (emote != null)
                {
                    try { await msg.AddReactionAsync(emote); } catch { }
                }
            }
        }
    }
}
