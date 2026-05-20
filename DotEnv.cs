using System;
using System.IO;

namespace DiscordReactionBot
{
    public static class DotEnv
    {
        public static void Load(string path = ".env")
        {
            if (!File.Exists(path))
                return;

            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex <= 0)
                    continue;

                var key = trimmed[..separatorIndex].Trim();
                var value = trimmed[(separatorIndex + 1)..].Trim();

                if (value.Length >= 2 && ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                                          (value.StartsWith("'") && value.EndsWith("'"))))
                {
                    value = value[1..^1];
                }

                if (Environment.GetEnvironmentVariable(key) == null)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
    }
}
