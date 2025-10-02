
//using Discord.Interactions;
//using Discord;
//using System.Text;
//using RS3ClanHelper.Services;

//namespace RS3ClanHelper.Modules
//{
//    // Top-level group: /admin
//    [Group("admin", "Admin & reporting tools")]
//    public class AdminExportModule : InteractionModuleBase<SocketInteractionContext>
//    {
//        private readonly StorageService _store; // kept for future growth; not used in simple export
//        public AdminExportModule(StorageService store) { _store = store; }

//        // /admin export  -> returns a CSV attachment with DiscordId, Username, Nickname
//        [SlashCommand("export", "Export Discord → RSN mappings / roster skeleton as CSV")]
//        [DefaultMemberPermissions(GuildPermission.Administrator)]
//        public async Task ExportAsync()
//        {
//            var sb = new StringBuilder();
//            sb.AppendLine("DiscordId,Username,Nickname");
//            foreach (var u in Context.Guild.Users)
//            {
//                var username = (u.Username ?? "").Replace("\"", "\"\"");
//                var nickname = (u.Nickname ?? "").Replace("\"", "\"\"");
//                sb.AppendLine($"{u.Id},\"{username}\",\"{nickname}\"");
//            }
//            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
//            await RespondWithFileAsync(new MemoryStream(bytes), "discord_roster.csv", "CSV export created.");
//        }
//    }
//}
