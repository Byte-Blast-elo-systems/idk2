using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordReactionBot.Models;

namespace DiscordReactionBot.Commands
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly Services.StorageService _storage;

        public CommandHandler(DiscordSocketClient client, Services.StorageService storage)
        {
            _client = client;
            _storage = storage;
        }

        public async Task HandleCommandAsync(SocketMessage msg, string prefix)
        {
            var content = msg.Content.Trim();
            if (!content.StartsWith(prefix, StringComparison.Ordinal)) return;

            var body = content[prefix.Length..].TrimStart();
            var parts = body.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            var cmd = parts[0].ToLowerInvariant();

            // permission check for commands
            var isAdmin = msg.Author.Id == _storage.Config.AdminId;
            var isAllowed = _storage.Allowed.Contains(msg.Author.Id);

            if (!(isAdmin || isAllowed))
            {
                await ReplyAsync(msg, "holy no perms, you can't use this command.");
                return;
            }

            switch (cmd)
            {
                case "ping":
                    await ReplyAsync(msg, $"pong {_client.Latency}ms");
                    break;

                case "help":
                    await HandleHelp(msg);
                    break;

                case "react":
                    await HandleReact(msg, parts);
                    break;

                case "preset":
                    await HandlePreset(msg, parts);
                    break;

                case "allow":
                    await HandleAllow(msg);
                    break;

                case "remove":
                    await HandleRemoveAllowed(msg);
                    break;

                case "block":
                    await HandleBlock(msg);
                    break;

                case "unblock":
                    await HandleUnblock(msg);
                    break;

                case "blockword":
                    await HandleBlockWord(msg, parts);
                    break;

                case "snipe":
                    await HandleSnipe(msg, parts);
                    break;
                case "prefix":
                    await HandlePrefix(msg, parts);
                    break;

                case "hi":
                    await HandleHi(msg);
                    break;
                case "fuck":
                    await HandleFuck(msg);
                    break;

                default:
                    // unknown
                    break;
            }
        }
        private async Task HandleFuck(SocketMessage msg)
        {
            var mention = msg.Author.Mention;
            await ReplyAsync(msg, $"{mention} no u weirdo ", $"/warn {mention} being a weirdo", Color.Red);
        }
        private async Task HandleHi(SocketMessage msg)
        {
            var mention = msg.Author.Mention;
            await ReplyAsync(msg, $"hi {mention}", "Hi there", Color.Green);
        }

        private Task ReplyAsync(SocketMessage original, string content, string title = "Annyoing ahh", Color? color = null)
        {
            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(content)
                .WithColor(color ?? Color.DarkBlue)
                .WithCurrentTimestamp()
                .WithFooter(footer => footer.Text = "Annyoing ahh — use ?help for bot commands")
                .Build();

            return original.Channel.SendMessageAsync(embed: embed, messageReference: new MessageReference(original.Id), allowedMentions: AllowedMentions.None);
        }

        private async Task HandleReact(SocketMessage msg, string[] parts)
        {
            if (parts.Length < 2)
            {
                await ReplyAsync(msg, "Usage: ?react <emoji(s)/preset> or ?react @user <...> or ?react off");
                return;
            }

            if (parts[1].Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                _storage.Config.Settings.ReactEnabled = false;
                _storage.Reactions.Clear();
                _storage.SaveConfig();
                _storage.SaveReactions();
                await ReplyAsync(msg, "Reactions disabled and rules cleared.");
                return;
            }

            // check for user mention or raw user ID
            var targetUser = GetTargetUserId(msg);
            var startIndex = targetUser.HasValue ? 2 : 1;

            if (parts.Length <= startIndex)
            {
                await ReplyAsync(msg, "No emojis or preset provided.");
                return;
            }

            var candidate = parts[startIndex];
            // if single token and matches preset name
            var preset = _storage.Presets.FirstOrDefault(p => p.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase));
            var emojis = preset != null ? preset.Emojis : parts.Skip(startIndex).ToList();

            if (targetUser.HasValue)
            {
                // user specific
                var existing = _storage.Reactions.FirstOrDefault(r => r.Type == ReactionType.User && r.UserId == targetUser.Value);
                if (existing != null) _storage.Reactions.Remove(existing);
                var rule = new ReactionRule { Type = ReactionType.User, UserId = targetUser.Value, Emojis = emojis };
               _storage.Reactions.Add(rule);
                _storage.SaveReactions();
                _storage.Config.Settings.ReactEnabled = true;
                _storage.SaveConfig();
                await ReplyAsync(msg, $"Set reactions for user <@{targetUser}>.");
            }
            else
            {
                // global
                var existing = _storage.Reactions.FirstOrDefault(r => r.Type == ReactionType.Global);
                if (existing != null) _storage.Reactions.Remove(existing);
                var rule = new ReactionRule { Type = ReactionType.Global, Emojis = emojis };
                _storage.Reactions.Add(rule);
                _storage.SaveReactions();
                _storage.Config.Settings.ReactEnabled = true;
                _storage.SaveConfig();
                await ReplyAsync(msg, "Set global reaction rule.");
            }
        }

        private async Task HandlePreset(SocketMessage msg, string[] parts)
        {
            if (parts.Length < 2)
            {
                await ReplyAsync(msg, "Usage: ?preset add <name> <emojis> | ?preset remove <name> | ?preset list");
                return;
            }
            var op = parts[1].ToLowerInvariant();
            if (op == "add")
            {
                if (parts.Length < 4)
                {
                    await ReplyAsync(msg, "Usage: ?preset add <name> <emojis>");
                    return;
                }
                var name = parts[2];
                var emojis = parts.Skip(3).ToList();
                var existing = _storage.Presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (existing != null) _storage.Presets.Remove(existing);
                _storage.Presets.Add(new Preset { Name = name, Emojis = emojis });
                _storage.SavePresets();
                await ReplyAsync(msg, $"Preset '{name}' saved.");
            }
            else if (op == "remove")
            {
                if (parts.Length < 3)
                {
                    await ReplyAsync(msg, "Usage: ?preset remove <name>");
                    return;
                }
                var name = parts[2];
                var existing = _storage.Presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    _storage.Presets.Remove(existing);
                    _storage.SavePresets();
                    await ReplyAsync(msg, $"Preset '{name}' removed.");
                }
                else await ReplyAsync(msg, $"Preset '{name}' not found.");
            }
            else if (op == "lis" || op == "list")
            {
                if (!_storage.Presets.Any())
                {
                    await ReplyAsync(msg, "No presets saved.");
                    return;
                }

                var lines = _storage.Presets
                    .Select(p => $"{p.Name} - {string.Join(' ', p.Emojis)}")
                    .ToList();
                var message = "Presets:\n" + string.Join("\n", lines);
                await ReplyAsync(msg, message);
            }
            else
            {
                await ReplyAsync(msg, "Usage: ?preset add <name> <emojis> | ?preset remove <name> | ?preset list");
            }
        }

        private async Task HandleHelp(SocketMessage msg)
        {
            var embed = new EmbedBuilder()
                .WithTitle("Annyoing ahh Command Help")
                .WithColor(Color.Green)
                .WithDescription("Use a configured prefix for all bot commands. Admin-only commands require the configured admin user.")
                .AddField("Reaction Rules", "`?react <emoji(s) OR preset>`\n`?react @user <emoji(s) OR preset>`\n`?react off`", true)
                .AddField("Presets", "`?preset add <name> <emoji(s)>`\n`?preset remove <name>`\n`?preset list`", true)
                .AddField("Utility", "`?ping`\n`?help`\n`?snipe [number]`\n`?prefix list`\n`?prefix add <prefix>`\n`?prefix remove <prefix>`", false)
                .AddField("User Management", "`?allow @user`\n`?remove @user`\n`?block @user`\n`?unblock @user`", false)
                .AddField("Safety", "`?blockword <word1> <word2> ...`", false)
                .AddField("Fun", "`?hi`\n`?fuck <user>`", false)
                .WithCurrentTimestamp()
                .WithFooter(footer => footer.Text = "Annyoing ahh help");

            await msg.Channel.SendMessageAsync(embed: embed.Build(), messageReference: new MessageReference(msg.Id), allowedMentions: AllowedMentions.None);
        }

        private async Task HandleSnipe(SocketMessage msg, string[] parts)
        {
            if (parts.Length == 1)
            {
                if (!_storage.DeletedMessages.Any())
                {
                    await ReplyAsync(msg, "No deleted messages available.");
                    return;
                }

                var latestDeleted = _storage.DeletedMessages[0];
                await ReplyWithDeletedMessageAsync(msg, latestDeleted, 1);
                return;
            }

            var option = parts[1].ToLowerInvariant();
            if (option == "clear")
            {
                if (msg.Author.Id != _storage.Config.AdminId)
                {
                    await ReplyAsync(msg, "this aint allowed by anyone dummy", color: Color.Red);
                    return;
                }

                _storage.DeletedMessages.Clear();
                _storage.SaveDeletedMessages();
                await ReplyAsync(msg, "cleared message history");
                return;
            }

            if (!int.TryParse(option, out var index) || index < 1)
            {
                await ReplyAsync(msg, "Usage: ?snipe [number] | ?snipe clear");
                return;
            }

            if (index > _storage.DeletedMessages.Count)
            {
                await ReplyAsync(msg, $"Only {_storage.DeletedMessages.Count} deleted messages are saved.", "Snipe Error", Color.Red);
                return;
            }

            var selectedDeleted = _storage.DeletedMessages[index - 1];
            await ReplyWithDeletedMessageAsync(msg, selectedDeleted, index);
        }

        private async Task HandlePrefix(SocketMessage msg, string[] parts)
        {
            if (msg.Author.Id != _storage.Config.AdminId)
            {
                await ReplyAsync(msg, "holy no perms, prefix config is admin-only.", "Nope", Color.Red);
                return;
            }

            if (parts.Length < 2)
            {
                await ReplyAsync(msg, "Usage: ?prefix list | ?prefix add <prefix> | ?prefix remove <prefix>");
                return;
            }

            var action = parts[1].ToLowerInvariant();
            if (action == "list")
            {
                var prefixes = _storage.Config.Settings.Prefixes;
                if (prefixes == null || prefixes.Count == 0)
                {
                    await ReplyAsync(msg, "No prefixes configured.");
                    return;
                }

                var lines = prefixes.Select(p => $"- {p}");
                var message = "Current prefixes:\n" + string.Join("\n", lines);
                await ReplyAsync(msg, message, "Prefix List", Color.Purple);
                return;
            }

            if (action == "add")
            {
                if (parts.Length < 3)
                {
                    await ReplyAsync(msg, "Usage: ?prefix add <prefix>");
                    return;
                }

                var newPrefix = parts[2];
                if (string.IsNullOrWhiteSpace(newPrefix))
                {
                    await ReplyAsync(msg, "Prefix cannot be empty.");
                    return;
                }

                var prefixes = _storage.Config.Settings.Prefixes;
                if (prefixes.Contains(newPrefix))
                {
                    await ReplyAsync(msg, "That prefix is already configured.");
                    return;
                }

                prefixes.Add(newPrefix);
                _storage.SaveConfig();
                await ReplyAsync(msg, $"Added prefix {newPrefix}.", "Prefix Added", Color.Green);
                return;
            }

            if (action == "remove")
            {
                if (parts.Length < 3)
                {
                    await ReplyAsync(msg, "Usage: ?prefix remove <prefix>");
                    return;
                }

                var removePrefix = parts[2];
                var prefixes = _storage.Config.Settings.Prefixes;
                if (!prefixes.Contains(removePrefix))
                {
                    await ReplyAsync(msg, "That prefix is not configured.");
                    return;
                }

                if (prefixes.Count == 1)
                {
                    await ReplyAsync(msg, "Cannot remove the last prefix.");
                    return;
                }

                prefixes.Remove(removePrefix);
                _storage.SaveConfig();
                await ReplyAsync(msg, $"Removed prefix {removePrefix}.", "Prefix Removed", Color.Orange);
                return;
            }

            await ReplyAsync(msg, "Usage: ?prefix list | ?prefix add <prefix> | ?prefix remove <prefix>");
        }

        private Task ReplyWithDeletedMessageAsync(SocketMessage original, Models.DeletedMessageRecord record, int index)
        {
            var channelName = string.IsNullOrWhiteSpace(record.ChannelName) ? record.ChannelId.ToString() : record.ChannelName;
            var embed = new EmbedBuilder()
                .WithTitle($"Deleted Message #{index}")
                .WithColor(Color.Orange)
                .WithDescription(string.IsNullOrWhiteSpace(record.Content) ? "(empty message)" : record.Content)
                .AddField("Author", record.AuthorTag, true)
                .AddField("Channel", channelName, true)
                .AddField("Deleted At", record.DeletedAt.ToString("u"), false)
                .WithCurrentTimestamp()
                .WithFooter(footer => footer.Text = "Annyoing ahh snipe");

            return original.Channel.SendMessageAsync(embed: embed.Build(), messageReference: new MessageReference(original.Id), allowedMentions: AllowedMentions.None);
        }

        private async Task HandleAllow(SocketMessage msg)
        {
            // only admin
            if (msg.Author.Id != _storage.Config.AdminId)
            {
                await ReplyAsync(msg, "ur to dumb to allow people");
                return;
            }
            var targetUserId = GetTargetUserId(msg);
            if (!targetUserId.HasValue)
            {
                await ReplyAsync(msg, "Mention a user or provide a user ID to allow.");
                return;
            }
            if (!_storage.Allowed.Add(targetUserId.Value))
            {
                await ReplyAsync(msg, "User already allowed.");
                return;
            }
            _storage.SaveAllowed();
            await ReplyAsync(msg, $"Allowed <@{targetUserId.Value}> to use commands.");
        }

        private async Task HandleRemoveAllowed(SocketMessage msg)
        {
            // only admin
            if (msg.Author.Id != _storage.Config.AdminId)
            {
                await ReplyAsync(msg, "holy dumb ahh only admin can remove allowed users");
                return;
            }
            var targetUserId = GetTargetUserId(msg);
            if (!targetUserId.HasValue)
            {
                await ReplyAsync(msg, "Mention a user or provide a user ID to remove.");
                return;
            }
            if (targetUserId.Value == _storage.Config.AdminId)
            {
                await ReplyAsync(msg, "can't unallow the admin dummy");
                return;
            }
            if (_storage.Allowed.Remove(targetUserId.Value))
            {
                _storage.SaveAllowed();
                await ReplyAsync(msg, $"Removed <@{targetUserId.Value}> from allowed users.");
            }
            else await ReplyAsync(msg, "User was not in allowed list.");
        }

        private async Task HandleBlock(SocketMessage msg)
        {
            if (msg.Author.Id != _storage.Config.AdminId && !_storage.Allowed.Contains(msg.Author.Id))
            {
                await ReplyAsync(msg, "holy no perms");
                return;
            }
            var targetUserId = GetTargetUserId(msg);
            if (!targetUserId.HasValue)
            {
                await ReplyAsync(msg, "Mention a user or provide a user ID to block.");
                return;
            }
            if (!_storage.Blocked.Add(targetUserId.Value))
            {
                await ReplyAsync(msg, "User already blocked.");
                return;
            }
            _storage.SaveBlocked();
            await ReplyAsync(msg, $"Blocked <@{targetUserId.Value}>.");
        }

        private async Task HandleUnblock(SocketMessage msg)
        {
            if (msg.Author.Id != _storage.Config.AdminId && !_storage.Allowed.Contains(msg.Author.Id))
            {
                await ReplyAsync(msg, "holy no perms");
                return;
            }
            var targetUserId = GetTargetUserId(msg);
            if (!targetUserId.HasValue)
            {
                await ReplyAsync(msg, "Mention a user or provide a user ID to unblock.");
                return;
            }
            if (_storage.Blocked.Remove(targetUserId.Value))
            {
                _storage.SaveBlocked();
                await ReplyAsync(msg, $"Unblocked <@{targetUserId.Value}>.");
            }
            else await ReplyAsync(msg, "User was not blocked.");
        }

        private ulong? GetTargetUserId(SocketMessage msg)
        {
            var mentioned = msg.MentionedUsers.FirstOrDefault();
            if (mentioned != null)
            {
                return mentioned.Id;
            }

            var parts = msg.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return null;

            var raw = parts[1].Trim();
            raw = raw.TrimStart('<', '@', '!').TrimEnd('>');
            return ulong.TryParse(raw, out var id) ? id : null;
        }

        private async Task HandleBlockWord(SocketMessage msg, string[] parts)
        {
            if (parts.Length < 2)
            {
                await ReplyAsync(msg, "Usage: ?blockword <word1> <word2> ...");
                return;
            }

            var blockWords = parts.Skip(1)
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(w => w.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            if (!blockWords.Any())
            {
                await ReplyAsync(msg, "Usage: ?blockword <word1> <word2> ...");
                return;
            }

            var addedWords = new List<string>();
            foreach (var word in blockWords)
            {
                if (!_storage.BlockWords.Any(existing => existing.Equals(word, StringComparison.OrdinalIgnoreCase)))
                {
                    _storage.BlockWords.Add(word);
                    addedWords.Add(word);
                }
            }

            if (!addedWords.Any())
            {
                await ReplyAsync(msg, "All provided words are already blocked.");
                return;
            }

            _storage.SaveBlockWords();
            await ReplyAsync(msg, $"Added blocked words: {string.Join(' ', addedWords)}");
        }
    }
}
