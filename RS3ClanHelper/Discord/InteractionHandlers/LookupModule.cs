using Discord.Interactions;
using RS3ClanHelper.Services;
using RS3ClanHelper.Models;

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
            // load config to get the connected clan name
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

            var m = roster.Members
                          .FirstOrDefault(x => string.Equals(x.DisplayName, rsn,
                              StringComparison.OrdinalIgnoreCase));

            if (m == null)
                await RespondAsync($"No member named **{rsn}** found in {cfg.ClanName}.");
            else
                await RespondAsync($"**{m.DisplayName}** — Rank: **{m.Rank}**, Clan XP: **{m.ClanXp:N0}**, Clan Kills: **{m.ClanKills:N0}**");
        }
    }
}
