using System.Linq;
using Discord;
using Discord.WebSocket;

public static class EmoteParser
{
    public static IEmote? ParseEmote(string token, DiscordSocketClient client)
    {
        // Try Discord custom emote format
        if (Emote.TryParse(token, out var emote)) return emote;

        // Try find emote by name in guilds
        if (token.StartsWith(":" ) && token.EndsWith(":"))
        {
            var name = token.Trim(':');
            foreach (var g in client.Guilds)
            {
                var found = g.Emotes.FirstOrDefault(e => e.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
                if (found != null) return found;
            }
        }

        // Fallback: attempt Unicode emoji
        try
        {
            return new Emoji(token);
        }
        catch
        {
            return null;
        }
    }
}
