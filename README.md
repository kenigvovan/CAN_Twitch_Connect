# CanTwitchConnect

A [Vintage Story](https://www.vintagestory.at/) mod that lets your Twitch chat vote on what happens to you in the game: spawn wolves, set you on fire, toss you into the sky, change the weather and more. Polls show up in-game as a tarot-card overlay with live vote counts.

**Download:** [mods.vintagestory.at/cantwitchconnect](https://mods.vintagestory.at/cantwitchconnect)

## How it works

1. A viewer types a command in chat, e.g. `!wolves`.
2. A poll starts. The bot posts the options and their aliases in chat, and the overlay appears in-game.
3. Viewers vote by typing an alias (`0`, `1`, `yes`, `no`, ...). Each viewer gets one vote.
4. When the timer runs out, the option with the most votes wins (ties are broken at random) and the action runs after a short delay.

`!help` in chat lists the available commands.

## Built-in commands

| Command | What it does |
|---|---|
| `!kill` | Kill the player |
| `!fullhealth` | Restore full health |
| `!halfhealth` | Cut health in half |
| `!wolves` | Spawn 0–3 wolves |
| `!bear` | Spawn a bear |
| `!drifters` | Spawn 0–3 drifters |
| `!toss` | Launch the player 15 / 30 / 60 blocks up |
| `!setonfire` | Set the player on fire |
| `!setday` / `!setnight` | Change the time of day |
| `!stoprain` / `!startrain` | Change the weather |
| `!rtp` | Random teleport 500 / 1000 / 1500 blocks away |
| `!giveitem` | Give flint, sticks or rocks |
| `!dropinventory` | Drop the whole inventory |
| `!shuffle` | Shuffle the hotbar |
| `!lightning` | Strike the player with lightning |

Every command can be changed, disabled or duplicated with different settings in the config.

## Setup

1. Install the mod (it is needed on both the server and the client).
2. Create an application at the [Twitch Developer Console](https://dev.twitch.tv/console/apps):
   - OAuth Redirect URL: `http://localhost`
   - Copy the **Client ID** and generate a **Client Secret**.
3. Get an authorization code. Log in as the channel account and open this URL (replace `YOUR_CLIENT_ID`):
   ```
   https://id.twitch.tv/oauth2/authorize?response_type=code&client_id=YOUR_CLIENT_ID&redirect_uri=http://localhost&scope=chat:read+chat:edit
   ```
   After you authorize, the browser is redirected to `http://localhost/?code=...`. The page will not load, which is fine: copy the `code` value from the address bar.
4. Start the game or server once so the config file is generated at `VintagestoryData/ModConfig/cantwitchconnect`, then fill in:
   ```json
   {
     "Channel": "your_channel_name",
     "ClientId": "...",
     "ClientSecret": "...",
     "AccessCode": "..."
   }
   ```
5. Restart. The mod exchanges the code for tokens, saves them to the config and refreshes them automatically. The access code is single-use and is cleared afterwards.

## Configuration

Global settings:

| Field | Default | Description |
|---|---|---|
| `GlobalCooldownSeconds` | `60` | Pause between polls |
| `ActionDelaySeconds` | `3` | Delay between the poll result and the action |
| `AnnounceIngame` | `true` | Post poll start/results to the in-game chat |
| `DefaultPlayerNames` | `[]` | Players targeted by actions. Empty means all online players |
| `WeatherOverrideSeconds` | `600` | How long a forced weather stays (`0` = forever) |
| `VoterWhitelist` / `VoterBlacklist` | — | Restrict who can vote and start polls |
| `OfflineDebug` | `false` | Run polls without connecting to Twitch, for testing |

Per-command settings (inside `Commands`):

| Field | Description |
|---|---|
| `Name` | Chat command name (without `!`) |
| `Kind` | Action type: `KillPlayers`, `HealthChange`, `SpawnEntity`, `TossPlayer`, `RtpPlayer`, `SetOnFire`, `ChangeWeather`, `CallChatCommand`, `GiveItem`, `DropInventory`, `ShuffleHotbar`, `LightningStrike` |
| `SecondsForVote` | Poll duration (default `30`) |
| `MinimumVotes` | Votes required for the poll to count |
| `CooldownSeconds` | Per-command cooldown |
| `SubscribersOnly` | Only subscribers (plus mods and the broadcaster) can start and vote |
| `Enabled` | Turn the command on or off |
| `PlayerNames` | Overrides `DefaultPlayerNames` for this command |
| `Answers` | Options: `AnswerName`, `LangCode`, `Aliases` (what viewers type), `TarotCard` (overlay texture) |

Kind-specific fields: `EntityCodes`, `Heights`, `Radius`, `HealthChangeType`, `WeatherChangeType`, `ItemCodes`, `Quantities`, `LightningRadius`, `CommandToCall` + `AllowedChatCommands`.

Moderators and the broadcaster always bypass the whitelist, blacklist and subscriber-only restrictions.

## Admin commands

Require the `controlserver` privilege.

| Command | Description |
|---|---|
| `/ctc pause` | Cancel the current poll and stop accepting new ones |
| `/ctc resume` | Resume polls |
| `/ctc trigger <name>` | Start a poll manually |
| `/ctc preview <name> [winnerIndex]` | Preview the overlay without running the action |
| `/ctc reload` | Reload the config from disk without restarting |

**F8** opens the debug panel: trigger polls, cast test votes and toggle commands on and off.