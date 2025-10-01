using System;
using System.Collections.Generic;
using RS3ClanHelper.Models;

namespace RS3ClanHelper.State
{
    public static class PendingStore
    {
        private static readonly Dictionary<string, PendingEntry> _store = new();
        private static readonly Random _rng = new();

        public static string Add(ulong guildId, ulong requestorId, List<RoleDelta> deltas)
        {
            var key = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{_rng.Next(1000, 9999)}";
            _store[key] = new PendingEntry { GuildId = guildId, RequestorId = requestorId, Deltas = deltas };
            return key;
        }
        public static bool TryGet(string key, out PendingEntry entry) => _store.TryGetValue(key, out entry!);
        public static void Remove(string key) { if (_store.ContainsKey(key)) _store.Remove(key); }
    }
}
