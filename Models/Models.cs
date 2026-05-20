using System;
using System.Collections.Generic;

namespace DiscordReactionBot.Models
{
    public class BotConfig
    {
        public ulong AdminId { get; set; }
        public BotSettings Settings { get; set; } = new BotSettings();
    }

    public class BotSettings
    {
        public bool ReactEnabled { get; set; } = true;
        public List<string> Prefixes { get; set; } = new List<string> { "?" };
    }

    public enum ReactionType
    {
        Global,
        User
    }

    public class ReactionRule
    {
        public ReactionType Type { get; set; }
        public ulong? UserId { get; set; }
        public List<string> Emojis { get; set; } = new List<string>();
    }

    public class Preset
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Emojis { get; set; } = new List<string>();
    }

    public class DeletedMessageRecord
    {
        public string Content { get; set; } = string.Empty;
        public string AuthorTag { get; set; } = string.Empty;
        public ulong AuthorId { get; set; }
        public ulong ChannelId { get; set; }
        public string ChannelName { get; set; } = string.Empty;
        public DateTimeOffset DeletedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class RulesConfig
    {
        public string Title { get; set; } = "Rules";
        public string Description { get; set; } = "No rules configured.";
        public string Color { get; set; } = "#3498db";
    }
}
