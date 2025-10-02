
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
        public LookupModule(IClanApiClient clan, SnapshotService snaps) { _clan = clan; _snapshots = snaps; }

        [SlashCommand("member", "Look up a clan member by RSN")]  // becomes /lookup member <rsn>
        public async Task LookupAsync([Summary(description: "RuneScape Name")] string rsn)
        {
            var roster = await _clan.FetchClanAsync("{clanName}");
            if (roster == null) { await RespondAsync("Clan roster unavailable."); return; }
            var m = roster.Members.FirstOrDefault(x => string.Equals(x.DisplayName, rsn, StringComparison.OrdinalIgnoreCase));
            if (m == null) { await RespondAsync($"No member named **{rsn}** found."); return; }
            await RespondAsync($"**{m.DisplayName}** — Rank: **{m.Rank}**, Clan XP: **{m.ClanXp:N0}**, Clan Kills: **{m.ClanKills:N0}**");
        }

        [SlashCommand("top_xp", "Top XP gainers in the last N days")]
        public async Task TopXpAsync([Summary(description:"Days window")] int days = 7)
        {
            var gains = _snapshots.ComputeGains(TimeSpan.FromDays(days)).Take(10).ToList();
            if (gains.Count == 0) { await RespondAsync("No gains recorded in that window yet."); return; }
            var lines = gains.Select((g,i)=> $"`{i+1:00}` **{g.Rsn}** +{g.Gain:N0} XP since {g.Since:u}");
            await RespondAsync(string.Join("\n", lines));
        }
    }
}
