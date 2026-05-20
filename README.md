# Annoying ahh Bot

A Discord bot that reacts to messages, supports presets, blocked words, allowed users, and live JSON config updates.

## Setup

1. Create a `.env` file in the project root.
2. Add your bot token and admin ID:

```env
DISCORD_TOKEN=YOUR_DISCORD_BOT_TOKEN
BOT_ADMIN_ID=YOUR_ADMIN_USER_ID
```

> Use your Discord user ID for `BOT_ADMIN_ID`.

## Build & Run

From the project root:

```bash
./build.sh
./publish/linux-x64/idk2
```

If you want to use `dotnet` directly:

```bash
dotnet build
dotnet run --project idk2.csproj
```

## Configuration

Config files are stored under `config/`:

- `botconfig.json` — bot settings and prefixes
- `allowed.json` — allowed user IDs
- `blocked.json` — blocked user IDs
- `blockwords.json` — blocked words
- `presets.json` — reaction presets
- `reactions.json` — reaction rules
- `rules.json` — rules embed content

Changes to these JSON files are reloaded while the bot is running.

## Common commands

- `?ping` — bot latency check
- `?help` — show command help
- `?rules` — display rules from `rules.json`
- `?react <emoji(s)>` — set reaction rules
- `?react @user <emoji(s)>` — set user-specific reaction rules
- `?react off` — disable reacting
- `?react on` — enable reacting
- `?react clear` — clear reaction rules
- `?react status` — show reaction status
- `?preset add <name> <emoji(s)>` — save a preset
- `?preset remove <name>` — delete a preset
- `?preset list` — list presets
- `?allow @user` — allow a user to use commands
- `?remove @user` — remove allowed user
- `?block @user` — block a user from reactions
- `?unblock @user` — allow a blocked user again
- `?blockword <word1> <word2> ...` — add blocked words
- `?snipe [number]` — show deleted message history
- `?snipe clear` — clear deleted message history
- `?prefix list` — list configured prefixes
- `?prefix add <prefix>` — add prefix
- `?prefix remove <prefix>` — remove prefix

## Notes

- Only the admin or allowed users can use commands.
- Reactions are applied to everyone unless blocked.
- `rules.json` controls the `?rules` embed title, description, and color.
