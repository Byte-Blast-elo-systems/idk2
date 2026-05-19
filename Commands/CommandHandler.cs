using System;
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

        public async Task HandleCommandAsync(SocketMessage msg)
        {
            var content = msg.Content.Trim();
            var parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            var cmd = parts[0].Substring(1).ToLowerInvariant(); // remove prefix

            // permission check for commands (except ping)
            var isAdmin = msg.Author.Id == _storage.Config.AdminId;
            var isAllowed = _storage.Allowed.Contains(msg.Author.Id);

            if (cmd != "ping" && !(isAdmin || isAllowed))
            {
                await msg.Channel.SendMessageAsync("You are not allowed to use bot commands.");
                return;
            }

            switch (cmd)
            {
                case "ping":
                    await msg.Channel.SendMessageAsync($"pong {_client.Latency}ms");
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

                default:
                    // unknown
                    break;
            }
        }

        private async Task HandleReact(SocketMessage msg, string[] parts)
        {
            if (parts.Length < 2)
            {
                await msg.Channel.SendMessageAsync("Usage: ?react <emoji(s)/preset> or ?react @user <...> or ?react off");
                return;
            }

            if (parts[1].Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                _storage.Config.Settings.ReactEnabled = false;
                _storage.Reactions.Clear();
                _storage.SaveConfig();
                _storage.SaveReactions();
                await msg.Channel.SendMessageAsync("Reactions disabled and rules cleared.");
                return;
            }

            // check for mentioned user
            var mentioned = msg.MentionedUsers.FirstOrDefault();
            int startIndex = 1;
            ulong? targetUser = null;
            if (mentioned != null)
            {
                targetUser = mentioned.Id;
                startIndex = 2;
            }

            if (parts.Length <= startIndex)
            {
                await msg.Channel.SendMessageAsync("No emojis or preset provided.");
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
                await msg.Channel.SendMessageAsync($"Set reactions for user <@{targetUser}>.");
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
                await msg.Channel.SendMessageAsync("Set global reaction rule.");
            }
        }

        private async Task HandlePreset(SocketMessage msg, string[] parts)
        {
            if (parts.Length < 2)
            {
                await msg.Channel.SendMessageAsync("Usage: ?preset add <name> <emojis> | ?preset remove <name>");
                return;
            }
            var op = parts[1].ToLowerInvariant();
            if (op == "add")
            {
                if (parts.Length < 4)
                {
                    await msg.Channel.SendMessageAsync("Usage: ?preset add <name> <emojis>");
                    return;
                }
                var name = parts[2];
                var emojis = parts.Skip(3).ToList();
                var existing = _storage.Presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (existing != null) _storage.Presets.Remove(existing);
                _storage.Presets.Add(new Preset { Name = name, Emojis = emojis });
                _storage.SavePresets();
                await msg.Channel.SendMessageAsync($"Preset '{name}' saved.");
            }
            else if (op == "remove")
            {
                if (parts.Length < 3)
                {
                    await msg.Channel.SendMessageAsync("Usage: ?preset remove <name>");
                    return;
                }
                var name = parts[2];
                var existing = _storage.Presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    _storage.Presets.Remove(existing);
                    _storage.SavePresets();
                    await msg.Channel.SendMessageAsync($"Preset '{name}' removed.");
                }
                else await msg.Channel.SendMessageAsync($"Preset '{name}' not found.");
            }
        }

        private async Task HandleAllow(SocketMessage msg)
        {
            // only admin
            if (msg.Author.Id != _storage.Config.AdminId)
            {
                await msg.Channel.SendMessageAsync("Only admin can add allowed users.");
                return;
            }
            var mentioned = msg.MentionedUsers.FirstOrDefault();
            if (mentioned == null)
            {
                await msg.Channel.SendMessageAsync("Mention a user to allow.");
                return;
            }
            if (!_storage.Allowed.Add(mentioned.Id))
            {
                await msg.Channel.SendMessageAsync("User already allowed.");
                return;
            }
            _storage.SaveAllowed();
            await msg.Channel.SendMessageAsync($"Allowed <@{mentioned.Id}> to use commands.");
        }

        private async Task HandleRemoveAllowed(SocketMessage msg)
        {
            // only admin
            if (msg.Author.Id != _storage.Config.AdminId)
            {
                await msg.Channel.SendMessageAsync("Only admin can remove allowed users.");
                return;
            }
            var mentioned = msg.MentionedUsers.FirstOrDefault();
            if (mentioned == null)
            {
                await msg.Channel.SendMessageAsync("Mention a user to remove.");
                return;
            }
            if (mentioned.Id == _storage.Config.AdminId)
            {
                await msg.Channel.SendMessageAsync("Cannot remove admin from allowed list.");
                return;
            }
            if (_storage.Allowed.Remove(mentioned.Id))
            {
                _storage.SaveAllowed();
                await msg.Channel.SendMessageAsync($"Removed <@{mentioned.Id}> from allowed users.");
            }
            else await msg.Channel.SendMessageAsync("User was not in allowed list.");
        }

        private async Task HandleBlock(SocketMessage msg)
        {
            if (msg.Author.Id != _storage.Config.AdminId && !_storage.Allowed.Contains(msg.Author.Id))
            {
                await msg.Channel.SendMessageAsync("You do not have permission to block users.");
                return;
            }
            var mentioned = msg.MentionedUsers.FirstOrDefault();
            if (mentioned == null)
            {
                await msg.Channel.SendMessageAsync("Mention a user to block.");
                return;
            }
            if (!_storage.Blocked.Add(mentioned.Id))
            {
                await msg.Channel.SendMessageAsync("User already blocked.");
                return;
            }
            _storage.SaveBlocked();
            await msg.Channel.SendMessageAsync($"Blocked <@{mentioned.Id}>.");
        }

        private async Task HandleUnblock(SocketMessage msg)
        {
            if (msg.Author.Id != _storage.Config.AdminId && !_storage.Allowed.Contains(msg.Author.Id))
            {
                await msg.Channel.SendMessageAsync("You do not have permission to unblock users.");
                return;
            }
            var mentioned = msg.MentionedUsers.FirstOrDefault();
            if (mentioned == null)
            {
                await msg.Channel.SendMessageAsync("Mention a user to unblock.");
                return;
            }
            if (_storage.Blocked.Remove(mentioned.Id))
            {
                _storage.SaveBlocked();
                await msg.Channel.SendMessageAsync($"Unblocked <@{mentioned.Id}>.");
            }
            else await msg.Channel.SendMessageAsync("User was not blocked.");
        }
    }
}
