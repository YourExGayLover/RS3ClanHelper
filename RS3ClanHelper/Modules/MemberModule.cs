using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using RS3ClanHelper.Services;
using RS3ClanHelper.State;
using RS3ClanHelper.Models;

namespace RS3ClanHelper.Modules
{
    [Group("member", "Clan member utilities")]
    public class MemberModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly AppState _state;
        private readonly IHiscoreClient _hiscores;
        private readonly IActivityTrackerService _tracker;
        private readonly IClanApiClient _clan;
        private readonly INameNormalizer _norm;

        public MemberModule(AppState state, IHiscoreClient hiscores, IActivityTrackerService tracker, IClanApiClient clan, INameNormalizer norm)
        {
            _state = state;
            _hiscores = hiscores;
            _tracker = tracker;
            _clan = clan;
            _norm = norm;
        }

        [SlashCommand("lookup", "Lookup a member by RSN")]
        public async Task Lookup([Summary("rsn", "RuneScape name")] string rsn)
        {
            await DeferAsync(ephemeral: true);

            // Hiscores basics
            var rank = await _hiscores.GetOverallRankAsync(rsn);
            var xp = await _hiscores.GetTotalXpAsync(rsn);

            // Clan data (rank, clan XP, kills, join date)
            string? clanRank = null;
            long? clanXp = null;
            long? clanKills = null;
            DateTimeOffset? joinDate = null;

            if (_state.Guilds.TryGetValue(Context.Guild.Id, out var cfg) && !string.IsNullOrWhiteSpace(cfg.ClanName))
            {
                var roster = await _clan.FetchClanAsync(cfg.ClanName!);
                if (roster != null)
                {
                    var key = _norm.Normalize(rsn);
                    var member = roster.Members.FirstOrDefault(m => _norm.Normalize(m.DisplayName) == key);
                    if (member != null)
                    {
                        clanRank = member.Rank;
                        clanXp = member.ClanXp;
                        clanKills = member.ClanKills;
                        joinDate = member.JoinDate;
                    }
                }
            }

            var embed = new EmbedBuilder()
                .WithTitle($":mag: Lookup: {rsn}")
                .AddField("Overall Rank", rank?.ToString("#,0") ?? "N/A", true)
                .AddField("Total XP", xp?.ToString("#,0") ?? "N/A", true)
                .AddField("Clan Rank", clanRank ?? "N/A", true)
                .AddField("Clan XP", clanXp?.ToString("#,0") ?? "N/A", true)
                .AddField("Kills", clanKills?.ToString("#,0") ?? "N/A", true)
                .AddField("Join Date", joinDate.HasValue ? joinDate.Value.ToString("yyyy-MM-dd") : "N/A", true)
                .WithFooter("Hiscores + clan roster")
                .WithCurrentTimestamp()
                .Build();

            await FollowupAsync(embed: embed, ephemeral: true);
        }
        [SlashCommand("top_xp", "Top XP gainers over a period")]
        public async Task TopXp([Summary("days", "Days to look back (default 7)")] int days = 7)
        {
            if (days < 1 || days > 90) { await RespondAsync("Days must be between 1 and 90.", ephemeral: true); return; }
            await DeferAsync();

            // Ensure we have a fresh snapshot today for fairness
            await _tracker.TakeSnapshotAsync(Context.Guild);

            // Build table from linked users
            var rsnByUser = _state.UserRsns
                .Where(kv => kv.Key.GuildId == Context.Guild.Id)
                .ToDictionary(kv => kv.Key.UserId, kv => kv.Value);

            var gains = await Task.WhenAll(rsnByUser.Select(async kv =>
            {
                var gain = await _tracker.GetXpGainAsync(Context.Guild.Id, kv.Value, days);
                var user = Context.Guild.GetUser(kv.Key);
                return (user, kv.Value, gain);
            }));

            var top = gains.Where(g => g.user != null).OrderByDescending(g => g.gain).Take(10).ToList();

            var embed = new EmbedBuilder()
                .WithTitle($":trophy: Top XP gainers (last {days} days)")
                .WithDescription(string.Join("\n", top.Select((g, i) => $"{i+1}. {g.user!.Mention} ({g.Item2}) — **{g.gain:#,0}** XP")))
                .WithCurrentTimestamp()
                .Build();

            await FollowupAsync(embed: embed);
        }

        [SlashCommand("set_inactive_config", "Configure inactive-member summary")]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task SetInactiveConfig(
            [Summary("days", "Days without XP to be considered inactive")] int days,
            [Summary("channel", "Channel for weekly summaries")] ITextChannel channel,
            [Summary("summary_day", "Day of week for summary")] DayOfWeek summaryDay = DayOfWeek.Monday,
            [Summary("hour", "Hour (0-23) to post the summary")] int hour = 9)
        {
            if (!_state.Guilds.TryGetValue(Context.Guild.Id, out var cfg))
            {
                cfg = new GuildConfig();
                _state.Guilds[Context.Guild.Id] = cfg;
            }
            cfg.InactiveDaysThreshold = Math.Clamp(days, 1, 180);
            cfg.InactiveSummaryChannelId = channel.Id;
            cfg.InactiveSummaryDay = summaryDay;
            cfg.InactiveSummaryHour = Math.Clamp(hour, 0, 23);

            await RespondAsync($":gear: Inactive summary set → **{cfg.InactiveDaysThreshold}** days, weekly on **{cfg.InactiveSummaryDay} {cfg.InactiveSummaryHour}:00**, channel {channel.Mention}.", ephemeral: true);
        }

        [SlashCommand("snapshot_now", "Force-take an XP snapshot now")]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task SnapshotNow()
        {
            await DeferAsync(ephemeral: true);
            var count = await _tracker.TakeSnapshotAsync(Context.Guild);
            await FollowupAsync($":bookmark_tabs: Snapshot recorded for **{count}** linked RSNs.", ephemeral: true);
        }
    }
}
