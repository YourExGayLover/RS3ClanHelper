
using Discord;
using Discord.Interactions;
using RS3ClanHelper.Models;
using RS3ClanHelper.Services;
using System.Text;

namespace RS3ClanHelper.Modules
{
    [Group("admin", "Admin & settings")]
    public class SettingsModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly StorageService _store;
        public SettingsModule(StorageService store) { _store = store; }

        [SlashCommand("settings", "View or edit bot settings")]
        public async Task SettingsAsync([Summary(description:"Option")] string option = "", [Summary(description:"Value")] string value = "")
        {
            var cfg = _store.Load<BotConfig>("botconfig.json");
            if (string.IsNullOrWhiteSpace(option))
            {
                await RespondAsync($"Clan: `{cfg.ClanName}` | AutoNick: `{cfg.AutoNicknameSync}` | AutoRoleOnJoin: `{cfg.AutoRoleSyncOnJoin}` | InactiveDays: `{cfg.InactiveDaysThreshold}`");
                return;
            }
            switch(option.ToLowerInvariant())
            {
                case "clan": cfg.ClanName = value; break;
                case "autonick": cfg.AutoNicknameSync = value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                case "autoroleonjoin": cfg.AutoRoleSyncOnJoin = value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                case "inactivedays": if (int.TryParse(value, out var d)) cfg.InactiveDaysThreshold = d; break;
            }
            _store.Save("botconfig.json", cfg);
            await RespondAsync("✅ Settings updated.", ephemeral:true);
        }
        [SlashCommand("export", "Export Discord → RSN mappings / roster skeleton as CSV")]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        public async Task ExportAsync()
        {
            var sb = new StringBuilder();
            sb.AppendLine("DiscordId,Username,Nickname");
            foreach (var u in Context.Guild.Users)
            {
                var username = (u.Username ?? "").Replace("\"", "\"\"");
                var nickname = (u.Nickname ?? "").Replace("\"", "\"\"");
                sb.AppendLine($"{u.Id},\"{username}\",\"{nickname}\"");
            }
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            await RespondWithFileAsync(new MemoryStream(bytes), "discord_roster.csv", "CSV export created.");
        }

    }
}
