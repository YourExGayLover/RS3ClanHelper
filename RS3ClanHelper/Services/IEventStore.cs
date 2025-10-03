using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RS3ClanHelper.Models;

namespace RS3ClanHelper.Services
{
    public interface IEventStore
    {
        Task SaveAsync(ClanEvent evt, CancellationToken ct = default);
        Task<ClanEvent?> LoadAsync(ulong guildId, string id, CancellationToken ct = default);
        Task<IReadOnlyList<ClanEvent>> ListUpcomingAsync(ulong guildId, CancellationToken ct = default);
        Task DeleteAsync(ulong guildId, string id, CancellationToken ct = default);
    }
}
