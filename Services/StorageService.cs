using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using DiscordReactionBot.Models;

namespace DiscordReactionBot.Services
{
    public class StorageService
    {
        private readonly string _dir;
        private readonly JsonSerializerOptions _opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
        };
        private readonly object _fileLock = new object();
        private readonly FileSystemWatcher _watcher;
        private readonly Dictionary<string, DateTime> _lastReloadTimes = new();
        private readonly Dictionary<string, (DateTime LastWrite, long Length)> _lastFileStats = new();
        private readonly Dictionary<string, Action> _reloadActions;

        public BotConfig Config { get; private set; } = new BotConfig();
        public HashSet<ulong> Allowed { get; private set; } = new HashSet<ulong>();
        public HashSet<ulong> Blocked { get; private set; } = new HashSet<ulong>();
        public List<string> BlockWords { get; private set; } = new List<string>();
        public List<DeletedMessageRecord> DeletedMessages { get; private set; } = new List<DeletedMessageRecord>();
        public List<Preset> Presets { get; private set; } = new List<Preset>();
        public List<ReactionRule> Reactions { get; private set; } = new List<ReactionRule>();
        public RulesConfig Rules { get; private set; } = new RulesConfig();

        public StorageService(string directory)
        {
            _dir = directory;
            Directory.CreateDirectory(_dir);
            _watcher = new FileSystemWatcher(_dir, "*.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;
            _watcher.Deleted += OnFileChanged;

            _reloadActions = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase)
            {
                ["botconfig.json"] = LoadConfig,
                ["allowed.json"] = LoadAllowed,
                ["blocked.json"] = LoadBlocked,
                ["blockwords.json"] = LoadBlockWords,
                ["deletedmessages.json"] = LoadDeletedMessages,
                ["presets.json"] = LoadPresets,
                ["reactions.json"] = LoadReactions,
                ["rules.json"] = LoadRules
            };
        }

        public Task LoadAllAsync()
        {
            foreach (var action in _reloadActions.Values)
                action();

            UpdateAllWriteTimes();
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
                var result = JsonSerializer.Deserialize<T>(txt);
                if (result == null)
                {
                    SaveAtomic(full, @default);
                    return @default;
                }

                return result;
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

                var oldEvents = _watcher.EnableRaisingEvents;
                _watcher.EnableRaisingEvents = false;
                try
                {
                    var txt = JsonSerializer.Serialize(data, _opts);
                    File.WriteAllText(tmp, txt, new System.Text.UTF8Encoding(false));
                    if (File.Exists(fullPath)) File.Delete(fullPath);
                    File.Move(tmp, fullPath);
                }
                finally
                {
                    _watcher.EnableRaisingEvents = oldEvents;
                }
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

        public void LoadBlockWords()
        {
            BlockWords = LoadOrDefault("blockwords.json", new List<string>());
        }
        public void SaveBlockWords() => SaveAtomic(Path.Combine(_dir, "blockwords.json"), BlockWords);

        public void LoadDeletedMessages()
        {
            DeletedMessages = LoadOrDefault("deletedmessages.json", new List<DeletedMessageRecord>());
        }
        public void SaveDeletedMessages() => SaveAtomic(Path.Combine(_dir, "deletedmessages.json"), DeletedMessages);

        public List<DeletedMessageRecord> ReadDeletedMessagesFromFile()
        {
            var full = Path.Combine(_dir, "deletedmessages.json");
            if (!File.Exists(full))
                return new List<DeletedMessageRecord>();
            try
            {
                var txt = File.ReadAllText(full);
                var result = JsonSerializer.Deserialize<List<DeletedMessageRecord>>(txt);
                return result ?? new List<DeletedMessageRecord>();
            }
            catch
            {
                return new List<DeletedMessageRecord>();
            }
        }

        public void LoadReactions()
        {
            Reactions = LoadOrDefault("reactions.json", new List<ReactionRule>());
        }
        public void SaveReactions() => SaveAtomic(Path.Combine(_dir, "reactions.json"), Reactions);

        public void LoadRules()
        {
            Rules = LoadOrDefault("rules.json", new RulesConfig());
        }

        public void SaveRules() => SaveAtomic(Path.Combine(_dir, "rules.json"), Rules);

        public void RefreshIfChanged()
        {
            lock (_fileLock)
            {
                foreach (var kv in _reloadActions)
                {
                    var full = Path.Combine(_dir, kv.Key);
                    var info = new FileInfo(full);
                    var currentStats = File.Exists(full)
                        ? (info.LastWriteTimeUtc, info.Length)
                        : (DateTime.MinValue, 0L);

                    if (!_lastFileStats.TryGetValue(kv.Key, out var knownStats) || knownStats != currentStats)
                    {
                        try
                        {
                            kv.Value();
                            _lastFileStats[kv.Key] = currentStats;
                        }
                        catch
                        {
                            // Ignore temporary invalid edits until file is valid.
                        }
                    }
                }
            }
        }

        private void UpdateAllWriteTimes()
        {
            lock (_fileLock)
            {
                foreach (var file in _reloadActions.Keys)
                {
                    var full = Path.Combine(_dir, file);
                    var info = new FileInfo(full);
                    _lastFileStats[file] = File.Exists(full)
                        ? (info.LastWriteTimeUtc, info.Length)
                        : (DateTime.MinValue, 0L);
                }
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Name))
                return;

            var name = Path.GetFileName(e.Name);
            if (string.IsNullOrEmpty(name))
                return;

            ReloadFileIfNeeded(name);
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Name))
                return;

            var name = Path.GetFileName(e.Name);
            if (string.IsNullOrEmpty(name))
                return;

            ReloadFileIfNeeded(name);
        }

        private void ReloadFileIfNeeded(string fileName)
        {
            var now = DateTime.UtcNow;
            lock (_fileLock)
            {
                if (_lastReloadTimes.TryGetValue(fileName, out var last) && (now - last).TotalMilliseconds < 250)
                    return;
                _lastReloadTimes[fileName] = now;
            }

            try
            {
                switch (fileName)
                {
                    case "botconfig.json": LoadConfig(); break;
                    case "allowed.json": LoadAllowed(); break;
                    case "blocked.json": LoadBlocked(); break;
                    case "blockwords.json": LoadBlockWords(); break;
                    case "deletedmessages.json": LoadDeletedMessages(); break;
                    case "presets.json": LoadPresets(); break;
                        case "reactions.json": LoadReactions(); break;
                    case "rules.json": LoadRules(); break;
                }
            }
            catch
            {
                // If the JSON is temporarily invalid while editing, ignore until it becomes valid again.
            }
        }
    }
}
