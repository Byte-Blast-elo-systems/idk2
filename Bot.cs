using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        private readonly Services.ReactionManager _reactionManager;
        private readonly Commands.CommandHandler _commands;
        private readonly Dictionary<ulong, Models.DeletedMessageRecord> _recentMessages = new();
        private readonly Queue<ulong> _recentMessageIds = new();
        private const int RecentMessageCacheSize = 200;

        public Bot(string token)
        {
            _token = token;
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.MessageContent | GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions
            };
            _client = new DiscordSocketClient(config);
            _storage = new Services.StorageService("config");
            _reactionManager = new Services.ReactionManager();
            _commands = new Commands.CommandHandler(_client, _storage, _reactionManager);
        }

        public async Task StartAsync()
        {
            _client.Log += LogAsync;
            _client.MessageReceived += MessageReceivedAsync;
            _client.MessageDeleted += MessageDeletedAsync;

            await _storage.LoadAllAsync();

            var token = _token;
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
        }

        private string? GetCommandPrefix(string content)
        {
            var prefixes = _storage.Config.Settings.Prefixes;
            if (prefixes == null || prefixes.Count == 0) return null;

            foreach (var prefix in prefixes.OrderByDescending(p => p.Length))
            {
                if (!string.IsNullOrEmpty(prefix) && content.StartsWith(prefix, StringComparison.Ordinal))
                    return prefix;
            }

            return null;
        }

        private Task LogAsync(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(SocketMessage msg)
        {
            if (msg.Author.IsBot) return;

            StoreRecentMessage(msg);

            // First handle commands if message starts with a configured prefix
            var commandPrefix = GetCommandPrefix(msg.Content);
            if (commandPrefix != null)
            {
                await _commands.HandleCommandAsync(msg, commandPrefix);
                return;
            }

            // Non-command messages: apply reaction rules to everyone
            // Blocking check
            if (_storage.Blocked.Contains(msg.Author.Id)) return;

            if (!_storage.Config.Settings.ReactEnabled) return;

            if (_storage.BlockWords.Any(word => msg.Content.Contains(word, StringComparison.OrdinalIgnoreCase))) return;

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

            var cancellationToken = _reactionManager.GetToken();
            _ = Task.Run(async () => await ReactToMessageAsync(msg, ruleToApply, cancellationToken));
        }

        private async Task ReactToMessageAsync(SocketMessage msg, Models.ReactionRule ruleToApply, CancellationToken cancellationToken)
        {
            foreach (var token in ruleToApply.Emojis)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var emote = EmoteParser.ParseEmote(token, _client);
                if (emote != null)
                {
                    try { await msg.AddReactionAsync(emote); } catch { }
                }
            }
        }

        private async Task MessageDeletedAsync(Cacheable<IMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel)
        {
            var messageId = cachedMessage.Id;
            Models.DeletedMessageRecord? record = null;

            var message = await cachedMessage.GetOrDownloadAsync();
            if (message != null && message.Author != null && !message.Author.IsBot)
            {
                var author = message.Author!;
                IMessageChannel? channel = message.Channel;
                if (channel == null)
                {
                    var fetchedChannel = await cachedChannel.GetOrDownloadAsync();
                    channel = fetchedChannel;
                }

                var channelId = channel?.Id ?? 0ul;
                var channelName = channel?.Name ?? string.Empty;

                record = new Models.DeletedMessageRecord
                {
                    Content = message.Content ?? string.Empty,
                    AuthorTag = author.ToString() ?? string.Empty,
                    AuthorId = author.Id,
                    ChannelId = channelId,
                    ChannelName = channelName,
                    Attachments = message.Attachments
                        .Select(a => new Models.AttachmentInfo
                        {
                            FileName = a.Filename,
                            Url = a.Url,
                            ContentType = a.ContentType ?? string.Empty
                        })
                        .ToList(),
                    DeletedAt = DateTimeOffset.UtcNow
                };
            }

            if (record == null && _recentMessages.TryGetValue(messageId, out var recent))
            {
                record = new Models.DeletedMessageRecord
                {
                    Content = recent.Content,
                    AuthorTag = recent.AuthorTag,
                    AuthorId = recent.AuthorId,
                    ChannelId = recent.ChannelId,
                    ChannelName = recent.ChannelName,
                    Attachments = recent.Attachments,
                    DeletedAt = DateTimeOffset.UtcNow
                };
            }

            if (record == null)
            {
                Console.WriteLine($"MessageDeleted event: record not found for deleted message {messageId}");
                return;
            }

            _storage.DeletedMessages.Insert(0, record);
            while (_storage.DeletedMessages.Count > 10)
            {
                _storage.DeletedMessages.RemoveAt(_storage.DeletedMessages.Count - 1);
            }
            _storage.SaveDeletedMessages();
            _recentMessages.Remove(messageId);
        }

        private void StoreRecentMessage(SocketMessage msg)
        {
            var record = new Models.DeletedMessageRecord
            {
                Content = msg.Content ?? string.Empty,
                AuthorTag = msg.Author.ToString() ?? string.Empty,
                AuthorId = msg.Author.Id,
                ChannelId = msg.Channel.Id,
                ChannelName = msg.Channel.Name,
                Attachments = msg.Attachments
                    .Select(a => new Models.AttachmentInfo
                    {
                        FileName = a.Filename,
                        Url = a.Url,
                        ContentType = a.ContentType ?? string.Empty
                    })
                    .ToList(),
                DeletedAt = DateTimeOffset.MinValue
            };

            _recentMessages[msg.Id] = record;
            _recentMessageIds.Enqueue(msg.Id);
            while (_recentMessageIds.Count > RecentMessageCacheSize)
            {
                var oldestId = _recentMessageIds.Dequeue();
                _recentMessages.Remove(oldestId);
            }
        }
    }
}
