using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord.WebSocket;
using RS3ClanHelper.Models;
using RS3ClanHelper.State;

namespace RS3ClanHelper.Services
{
    public class ActivityTrackerService : IActivityTrackerService
    {
        private readonly AppState _state;
        private readonly IHiscoreClient _hiscores;
        private readonly IActivityStore _store;
        private readonly INameNormalizer _norm;

        public ActivityTrackerService(AppState state, IHiscoreClient hiscores, IActivityStore store, INameNormalizer norm)
        {
            _state = state;
            _hiscores = hiscores;
            _store = store;
            _norm = norm;
        }

        public async Task<int> TakeSnapshotAsync(SocketGuild guild, CancellationToken ct = default)
        {
            var rsns = _state.UserRsns
                .Where(kv => kv.Key.GuildId == guild.Id)
                .Select(kv => kv.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var dict = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var rsn in rsns)
            {
                var xp = await _hiscores.GetTotalXpAsync(rsn, ct);
                if (xp is long v)
                    dict[_norm.Normalize(rsn)] = v;
            }

            var snap = new ActivitySnapshot
            {
                TakenAt = DateTimeOffset.UtcNow,
                TotalXpByRsn = dict
            };
            await _store.SaveSnapshotAsync(guild.Id, snap, ct);
            return dict.Count;
        }

        public async Task<long> GetXpGainAsync(ulong guildId, string rsn, int daysBack)
        {
            var end = DateTimeOffset.UtcNow;
            var start = end.AddDays(-daysBack);
            var list = await _store.LoadSnapshotsAsync(guildId, start, end);
            var key = _norm.Normalize(rsn);
            if (list.Count == 0) return 0;

            var first = list.First();
            var last = list.Last();
            var startXp = first.TotalXpByRsn.TryGetValue(key, out var a) ? a : 0;
            var endXp = last.TotalXpByRsn.TryGetValue(key, out var b) ? b : startXp;
            return Math.Max(0, endXp - startXp);
        }
    }
}
