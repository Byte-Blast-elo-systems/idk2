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
}
