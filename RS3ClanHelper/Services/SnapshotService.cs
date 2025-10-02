
using RS3ClanHelper.Models;
using System.Collections.Concurrent;

namespace RS3ClanHelper.Services
{
    public class SnapshotService
    {
        private readonly StorageService _store;
        private readonly HiscoresClient _hiscores;
        private readonly IClanApiClient _clan;
        private readonly ConcurrentDictionary<string, List<HiscoreSnapshot>> _cache = new();
        public SnapshotService(StorageService store, HiscoresClient hiscores, IClanApiClient clan)
        {
            _store = store; _hiscores = hiscores; _clan = clan;
            // load cached snapshots
            // by clan member rsn
        }

        public async Task<int> SnapshotClanAsync(string clanName, CancellationToken ct = default)
        {
            var roster = await _clan.FetchClanAsync(clanName, ct);
            if (roster == null) return 0;

            var ts = DateTime.UtcNow;
            int count = 0;
            foreach (var m in roster.Members)
            {
                var xp = await _hiscores.GetTotalXpAsync(m.DisplayName, ct);
                if (xp is null) continue;
                var list = _cache.GetOrAdd(m.DisplayName.ToLowerInvariant(), _ => new List<HiscoreSnapshot>());
                list.Add(new HiscoreSnapshot(m.DisplayName, xp.Value, ts));
                count++;
            }

            // persist
            var path = _store.PathJoin("snapshots.json");
            var flat = _cache.ToDictionary(kv => kv.Key, kv => kv.Value);
            System.IO.File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(flat, new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            return count;
        }

        public List<XpGain> ComputeGains(TimeSpan period)
        {
            var since = DateTime.UtcNow - period;
            var gains = new List<XpGain>();
            foreach (var (key, list) in _cache)
            {
                var sorted = list.OrderBy(s => s.Timestamp).ToList();
                var recent = sorted.Where(s => s.Timestamp >= since).ToList();
                if (recent.Count >= 2)
                {
                    var gain = recent.Last().TotalXp - recent.First().TotalXp;
                    if (gain != 0) gains.Add(new XpGain(recent.Last().Rsn, gain, recent.First().Timestamp));
                }
            }
            return gains.OrderByDescending(g => g.Gain).ToList();
        }
    }
}
