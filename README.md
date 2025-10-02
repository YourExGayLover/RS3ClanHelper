# RS3 Clan Role Sync Bot

A Discord bot written in C# / .NET 8 that syncs RuneScape 3 clan ranks with Discord roles. It fetches your clan roster and provides tools to audit & sync roles, schedule automatic updates, and track member activity.

---

## ✨ Features

- **Clan ↔ Discord role sync**
  - Link a Discord server to an RS3 clan and keep rank roles up to date.
  - Create standard RS3 rank roles with one command.
  - Audit vs. apply changes, or schedule periodic syncs with optional summaries.
  - Ping unmatched users so they can fix their display names or set their RSN.
- **Member tools**
  - Quick **member lookup** (overall rank & total XP from hiscores; clan data if available from roster).
  - **XP/Activity leaderboards** for configurable time spans.
  - **Inactive tracker**: weekly posts tagging members who haven’t gained XP in N days.
  - Manual **snapshots** to seed/refresh XP tracking.

---

## 🧭 Slash Commands

### `/clan …` (server admin)

| Command | Arguments | What it does |
|---|---|---|
| `/clan connect` | `<ClanName>` | Link this Discord server to the named RS3 clan. |
| `/clan create_rank_roles` | — | Create standard RS3 rank roles in the server. |
| `/clan set_rsn` | `@User` `RSN` | Link a Discord user to their RuneScape name for reliable matching. |
| `/clan audit_roles` | — | Show deltas between Discord roles and clan ranks (dry-run). |
| `/clan sync_now` | — | Immediately apply role updates. |
| `/clan schedule_sync` | `<hours>` `[summary_channel]` | Schedule a sync every N hours; optionally post a summary. |
| `/clan stop_sync` | — | Stop the scheduled sync task. |
| `/clan ping_unmatched` | `[channel]` `[message]` | Mention users not found in the roster with a custom message. |

### `/member …` (member utilities)

| Command | Arguments | What it does |
|---|---|---|
| `/member lookup` | `<rsn>` | Lookup a RuneScape name: overall rank & total XP (hiscores), clan rank/XP/kills/join date (if roster provides them). |
| `/member top_xp` | `[days=7]` | Show the top XP gainers over the last N days (default 7). |
| `/member set_inactive_config` | `<days>` `<channel>` `[summary_day]` `[hour]` | Configure inactive-member summary (0 XP gain for N days) and post channel. |
| `/member snapshot_now` | — | Take an XP snapshot immediately. |

---

## ⚙️ Requirements

- **.NET 8 SDK**
- Discord application & bot token
- Privileged intent: **Server Members**
- Permissions:
  - Manage Roles
  - Read/View Channels
  - Send Messages
  - Use Slash Commands
- Ensure the bot’s role is above RS3 rank roles in your Discord role list.

---

## 🚀 Setup

```bash
git clone https://github.com/YourExGayLover/RS3ClanHelper.git
cd RS3ClanHelper/RS3ClanHelper
dotnet build
