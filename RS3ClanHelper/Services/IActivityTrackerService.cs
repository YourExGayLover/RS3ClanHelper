using System.Threading;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace RS3ClanHelper.Services
{
    public interface IActivityTrackerService
    {
        Task<int> TakeSnapshotAsync(SocketGuild guild, CancellationToken ct = default);
        Task<long> GetXpGainAsync(ulong guildId, string rsn, int daysBack);
    }
}
