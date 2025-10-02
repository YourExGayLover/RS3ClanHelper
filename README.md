# RS3 Clan Role Sync Bot

A Discord bot written in **C# / .NET 8** that syncs RuneScape 3 clan ranks with Discord roles.  
It uses the official RS3 `members_lite.ws` API to fetch clan data and provides slash commands to manage, audit, and synchronize Discord roles with clan ranks.

---

## ✨ Features
- 🔗 **/clan connect `<ClanName>`** – Link your Discord server to an RS3 clan.  
- 🛡️ **/clan create_rank_roles** – Auto-create all RS3 rank roles.  
- 👤 **/clan set_rsn `@User RSN`** – Link a Discord user to their RuneScape Name.  
- 📊 **/clan audit_roles** – Show mismatches between Discord and clan ranks, with option to apply fixes.  
- ⚡ **/clan sync_now** – Immediately update Discord roles to match clan ranks.  
- ⏰ **/clan schedule_sync `<hours>` [#channel]** – Schedule automatic syncs every N hours, with optional summary posts.  
- ⏹️ **/clan stop_sync** – Cancel scheduled syncs.  
- 📣 **/clan ping_unmatched [#channel] [message]** – Mention users not found in the clan roster and prompt them to update their display names.

---

## 📋 Requirements
- .NET 8 SDK  
- A Discord bot application with:
  - **Bot Token** (from [Discord Developer Portal](https://discord.com/developers/applications))  
  - **Privileged Gateway Intents → Server Members** enabled  
  - Invited with permissions:  
    - Manage Roles  
    - Read Messages/View Channels  
    - Send Messages  
    - Use Slash Commands  

---

## 🚀 Setup
1. Clone this repo:
   ```bash
   git clone https://github.com/YOUR-USERNAME/rs3-clan-role-sync-bot.git
   cd rs3-clan-role-sync-bot


## New Features
- /clan lookup <rsn>
- Inactive tracker
- XP leaderboards via snapshots
- Role sync on join & nickname sync
- Event creation & RSVP
- Settings command and CSV export
- Alias support scaffolding
- Optional RuneMetrics notifications (stub)
