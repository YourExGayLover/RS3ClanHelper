using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.WebSocket;
using RS3ClanHelper.Models;

namespace RS3ClanHelper.Services
{
    public interface IRoleSyncService
    {
        Task<(IReadOnlyList<RoleDelta> deltas, IReadOnlyList<SocketGuildUser> unmatched)> AuditAsync(SocketGuild guild, string clanName);
        Task<int> ApplyAsync(SocketGuild guild, IEnumerable<RoleDelta> deltas);
        Task<(int changed, IReadOnlyList<SocketGuildUser> unmatched)> SyncAsync(SocketGuild guild, string clanName);
    }
}
