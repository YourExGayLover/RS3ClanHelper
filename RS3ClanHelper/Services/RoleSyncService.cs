using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;
using RS3ClanHelper.Models;

namespace RS3ClanHelper.Services
{
    public class RoleSyncService : IRoleSyncService
    {
        private readonly IClanApiClient _api;
        private readonly INameNormalizer _norm;

        private static readonly string[] RankNames = new[] {
            "Owner","Deputy Owner","Overseer","Coordinator","Organiser","Admin",
            "General","Captain","Lieutenant","Sergeant","Corporal","Recruit"
        };

        public RoleSyncService(IClanApiClient api, INameNormalizer norm)
        {
            _api = api; _norm = norm;
        }

        public async Task<(IReadOnlyList<RoleDelta> deltas, IReadOnlyList<SocketGuildUser> unmatched)> AuditAsync(SocketGuild guild, string clanName)
        {
            var roster = await _api.FetchClanAsync(clanName);
            var deltas = new List<RoleDelta>();
            var unmatched = new List<SocketGuildUser>();
            if (roster == null) return (deltas, unmatched);

            var rsnToRank = roster.Members
                .GroupBy(m => _norm.Normalize(m.DisplayName))
                .ToDictionary(g => g.Key, g => g.First().Rank);

            foreach (var user in guild.Users)
            {
                if (user.IsBot) continue;
                var rsn = user.Nickname ?? user.Username;
                var key = _norm.Normalize(rsn);
                if (!rsnToRank.TryGetValue(key, out var rank))
                { unmatched.Add(user); continue; }

                var desired = guild.Roles.FirstOrDefault(r => r.Name.Equals(rank, StringComparison.OrdinalIgnoreCase));
                if (desired == null) continue;

                var hasDesired = user.Roles.Any(r => r.Id == desired.Id);
                var otherRankRoles = user.Roles
                    .Where(r => RankNames.Any(n => n.Equals(r.Name, StringComparison.OrdinalIgnoreCase)) && r.Id != desired.Id)
                    .Select(r => r.Id).ToList();
                if (!hasDesired || otherRankRoles.Count > 0)
                    deltas.Add(new RoleDelta(user.Id, desired.Name, desired.Id, otherRankRoles));
            }

            return (deltas, unmatched);
        }

        public async Task<int> ApplyAsync(SocketGuild guild, IEnumerable<RoleDelta> deltas)
        {
            int changed = 0;
            foreach (var d in deltas)
            {
                var user = guild.GetUser(d.UserId);
                if (user == null) continue;
                var desired = guild.GetRole(d.DesiredRoleId);
                var toRemove = d.RemoveRoleIds.Select(guild.GetRole).Where(r => r != null)!.Cast<SocketRole>().ToList();
                try
                {
                    if (!user.Roles.Any(r => r.Id == desired.Id))
                        await user.AddRoleAsync(desired);
                    if (toRemove.Count > 0)
                        await user.RemoveRolesAsync(toRemove);
                    changed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Apply error for {user?.Username}: {ex.Message}");
                }
            }
            return changed;
        }

        public async Task<(int changed, IReadOnlyList<SocketGuildUser> unmatched)> SyncAsync(SocketGuild guild, string clanName)
        {
            var (deltas, unmatched) = await AuditAsync(guild, clanName);
            var changed = await ApplyAsync(guild, deltas);
            return (changed, unmatched);
        }
    }
}
