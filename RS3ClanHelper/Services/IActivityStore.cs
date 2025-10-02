using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RS3ClanHelper.Models;

namespace RS3ClanHelper.Services
{
    public interface IActivityStore
    {
        Task SaveSnapshotAsync(ulong guildId, ActivitySnapshot snapshot, CancellationToken ct = default);
        Task<ActivitySnapshot?> LoadLatestSnapshotAsync(ulong guildId, CancellationToken ct = default);
        Task<IReadOnlyList<ActivitySnapshot>> LoadSnapshotsAsync(ulong guildId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
    }
}
