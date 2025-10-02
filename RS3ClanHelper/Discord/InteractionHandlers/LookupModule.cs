using Discord.Interactions;
using RS3ClanHelper.Services;
using RS3ClanHelper.Models;
using System.Globalization;
using System.Text;

namespace RS3ClanHelper.Modules
{
    [Group("lookup", "Member lookup & leaderboards")]
    public class LookupModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IClanApiClient _clan;
        private readonly SnapshotService _snapshots;
        private readonly StorageService _store;

        public LookupModule(IClanApiClient clan, SnapshotService snaps, StorageService store)
        {
            _clan = clan;
            _snapshots = snaps;
            _store = store;
        }

        [SlashCommand("member", "Look up a clan member by RSN")]
        public async Task LookupAsync(string rsn)
        {
            var cfg = _store.Load<BotConfig>("botconfig.json");
            if (string.IsNullOrWhiteSpace(cfg.ClanName))
            {
                await RespondAsync("❌ No clan is configured. Use `/admin settings clan <ClanName>` first.");
                return;
            }

            var roster = await _clan.FetchClanAsync(cfg.ClanName);
            if (roster == null)
            {
                await RespondAsync($"❌ Could not fetch clan members for **{cfg.ClanName}**.");
                return;
            }

            // Normalization (spaces/underscores, diacritics, lowercase)
            string Normalize(string s) =>
                string.IsNullOrEmpty(s) ? "" :
                new string(s.Trim()
                             .Replace('\u00A0', ' ')
                             .Normalize(NormalizationForm.FormKC)
                             .ToLowerInvariant()
                             .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                             .ToArray());

            var target = Normalize(rsn);
            var targetSpacesToUnders = target.Replace(" ", "_");
            var targetUndersToSpaces = target.Replace("_", " ");

            ClanMember? found =
                roster.Members.FirstOrDefault(m => Normalize(m.DisplayName) == target) ??
                roster.Members.FirstOrDefault(m =>
                {
                    var dn = Normalize(m.DisplayName);
                    return dn.Replace(" ", "_") == targetSpacesToUnders ||
                           dn.Replace("_", " ") == targetUndersToSpaces;
                }) ??
                roster.Members.FirstOrDefault(m =>
                    string.Equals(m.DisplayName?.Trim(), rsn.Trim(), StringComparison.OrdinalIgnoreCase));

            if (found == null)
            {
                await RespondAsync($"No member named **{rsn}** found in **{cfg.ClanName}**.");
                return;
            }

            await RespondAsync($"**{found.DisplayName}** — Rank: **{found.Rank}**, Clan XP: **{found.ClanXp:N0}**, Clan Kills: **{found.ClanKills:N0}**");
        }
    }
}
