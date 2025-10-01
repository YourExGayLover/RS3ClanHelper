using System.Collections.Generic;
using RS3ClanHelper.Models;

namespace RS3ClanHelper.State
{
    public class AppState
    {
        public readonly Dictionary<ulong, GuildConfig> Guilds = new();
        public readonly Dictionary<(ulong GuildId, ulong UserId), string> UserRsns = new();
        public readonly Dictionary<ulong, SyncJob> SyncJobs = new(); // guildId -> job
    }
}
