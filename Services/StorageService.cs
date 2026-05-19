using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DiscordReactionBot.Models;

namespace DiscordReactionBot.Services
{
    public class StorageService
    {
        private readonly string _dir;
        private readonly JsonSerializerOptions _opts = new JsonSerializerOptions { WriteIndented = true };
        private readonly object _fileLock = new object();

        public BotConfig Config { get; private set; } = new BotConfig();
        public HashSet<ulong> Allowed { get; private set; } = new HashSet<ulong>();
        public HashSet<ulong> Blocked { get; private set; } = new HashSet<ulong>();
        public List<Preset> Presets { get; private set; } = new List<Preset>();
        public List<ReactionRule> Reactions { get; private set; } = new List<ReactionRule>();

        public StorageService(string directory)
        {
            _dir = directory;
            Directory.CreateDirectory(_dir);
        }

        public Task LoadAllAsync()
        {
            LoadConfig();
            LoadAllowed();
            LoadBlocked();
            LoadPresets();
            LoadReactions();
            return Task.CompletedTask;
        }

        private T LoadOrDefault<T>(string path, T @default)
        {
            var full = Path.Combine(_dir, path);
            if (!File.Exists(full))
            {
                SaveAtomic(full, @default);
                return @default;
            }
            try
            {
                var txt = File.ReadAllText(full);
                return JsonSerializer.Deserialize<T>(txt) ?? @default;
            }
            catch
            {
                // If corrupted, overwrite with default
                SaveAtomic(full, @default);
                return @default;
            }
        }

        private void SaveAtomic<T>(string fullPath, T data)
        {
            lock (_fileLock)
            {
                var tmp = fullPath + ".tmp";
                var dir = Path.GetDirectoryName(fullPath) ?? _dir;
                Directory.CreateDirectory(dir);
                var txt = JsonSerializer.Serialize(data, _opts);
                File.WriteAllText(tmp, txt);
                if (File.Exists(fullPath)) File.Delete(fullPath);
                File.Move(tmp, fullPath);
            }
        }

        public void LoadConfig()
        {
            Config = LoadOrDefault("botconfig.json", new BotConfig());
            // fallback to env for admin
            if (Config.AdminId == 0)
            {
                var env = Environment.GetEnvironmentVariable("BOT_ADMIN_ID");
                if (ulong.TryParse(env, out var id)) Config.AdminId = id;
            }
        }

        public void SaveConfig() => SaveAtomic(Path.Combine(_dir, "botconfig.json"), Config);

        public void LoadAllowed()
        {
            var list = LoadOrDefault("allowed.json", new List<ulong>());
            Allowed = new HashSet<ulong>(list);
        }
        public void SaveAllowed() => SaveAtomic(Path.Combine(_dir, "allowed.json"), Allowed.ToList());

        public void LoadBlocked()
        {
            var list = LoadOrDefault("blocked.json", new List<ulong>());
            Blocked = new HashSet<ulong>(list);
        }
        public void SaveBlocked() => SaveAtomic(Path.Combine(_dir, "blocked.json"), Blocked.ToList());

        public void LoadPresets()
        {
            Presets = LoadOrDefault("presets.json", new List<Preset>());
        }
        public void SavePresets() => SaveAtomic(Path.Combine(_dir, "presets.json"), Presets);

        public void LoadReactions()
        {
            Reactions = LoadOrDefault("reactions.json", new List<ReactionRule>());
        }
        public void SaveReactions() => SaveAtomic(Path.Combine(_dir, "reactions.json"), Reactions);
    }
}
