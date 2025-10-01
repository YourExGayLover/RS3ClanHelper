using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using RS3ClanHelper.State;
using RS3ClanHelper.Services;
using RS3ClanHelper.Models;
using RS3ClanHelper.Utils;

namespace RS3ClanHelper.Modules
{
    [Group("clan", "RS3 clan tools")]
    public class ClanModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly AppState _state;
        private readonly IClanApiClient _api;
        private readonly IRoleSyncService _sync;
        private readonly IScheduledSyncService _sched;
        private readonly INameNormalizer _norm;

        private static readonly string[] RankNames = new[] {
            "Owner","Deputy Owner","Overseer","Coordinator","Organiser","Admin",
            "General","Captain","Lieutenant","Sergeant","Corporal","Recruit"
        };

        public ClanModule(AppState state, IClanApiClient api, IRoleSyncService sync, IScheduledSyncService sched, INameNormalizer norm)
        {
            _state = state; _api = api; _sync = sync; _sched = sched; _norm = norm;
        }

        private GuildConfig GetCfg()
        {
            if (!_state.Guilds.TryGetValue(Context.Guild.Id, out var cfg))
                _state.Guilds[Context.Guild.Id] = cfg = new GuildConfig();
            return cfg;
        }

        [SlashCommand("connect", "Connect this server to your RS3 clan by name")]
        public async Task ConnectClan([Summary(description: "Exact clan name as shown in-game")] string clanName)
        {
            await DeferAsync(ephemeral: true);
            var cfg = GetCfg();
            cfg.ClanName = clanName.Trim();
            await FollowupAsync($"✅ Connected to RS3 clan: **{cfg.ClanName}**", ephemeral: true);
        }

        [SlashCommand("create_rank_roles", "Create RS3 rank roles in this server if missing")]
        [DefaultMemberPermissions(GuildPermission.ManageRoles)]
        public async Task CreateRankRoles()
        {
            await DeferAsync(ephemeral: true);
            var created = new List<string>();
            foreach (var rn in RankNames)
            {
                var role = Context.Guild.Roles.FirstOrDefault(r => r.Name.Equals(rn, System.StringComparison.OrdinalIgnoreCase));
                if (role == null)
                {
                    var newRole = await Context.Guild.CreateRoleAsync(rn, GuildPermissions.None, isHoisted: false, isMentionable: false);
                    created.Add(newRole.Name);
                }
            }
            await FollowupAsync(created.Count == 0 ? "All RS3 rank roles already exist." : $"Created roles: {string.Join(", ", created)}", ephemeral: true);
        }

        [SlashCommand("set_rsn", "Link a Discord user to their RSN (improves matching)")]
        public async Task SetRsn([Summary(description: "Discord user to link")] SocketGuildUser user,
                                 [Summary(description: "RuneScape Name")] string rsn)
        {
            _state.UserRsns[(Context.Guild.Id, user.Id)] = rsn.Trim();
            await RespondAsync($"Linked {user.Mention} to RSN **{rsn}**.", ephemeral: true);
        }

        [SlashCommand("audit_roles", "Compare Discord roles against RS3 clan ranks")]
        public async Task AuditRoles()
        {
            await DeferAsync(ephemeral: true);
            var cfg = GetCfg();
            if (string.IsNullOrWhiteSpace(cfg.ClanName))
            { await FollowupAsync("❌ No clan connected. Use /clan connect <name> first.", ephemeral: true); return; }

            var roster = await _api.FetchClanAsync(cfg.ClanName);
            if (roster == null)
            { await FollowupAsync($"❌ Could not fetch clan members for **{cfg.ClanName}**.", ephemeral: true); return; }

            var rsnToRank = roster.Members
                .GroupBy(m => _norm.Normalize(m.DisplayName))
                .ToDictionary(g => g.Key, g => g.First().Rank);

            var deltas = new List<RoleDelta>();
            var unmatchedUsers = new List<SocketGuildUser>();

            foreach (var user in Context.Guild.Users)
            {
                if (user.IsBot) continue;
                var rsn = _state.UserRsns.TryGetValue((Context.Guild.Id, user.Id), out var mapped)
                    ? mapped
                    : (user.Nickname ?? user.Username);

                if (!rsnToRank.TryGetValue(_norm.Normalize(rsn), out var rank))
                { unmatchedUsers.Add(user); continue; }

                var desired = Context.Guild.Roles.FirstOrDefault(r => r.Name.Equals(rank, System.StringComparison.OrdinalIgnoreCase));
                if (desired == null) continue;

                var hasDesired = user.Roles.Any(r => r.Id == desired.Id);
                var otherRankRoles = user.Roles
                    .Where(r => RankNames.Any(n => n.Equals(r.Name, System.StringComparison.OrdinalIgnoreCase)) && r.Id != desired.Id)
                    .Select(r => r.Id).ToList();
                if (!hasDesired || otherRankRoles.Count > 0)
                    deltas.Add(new RoleDelta(user.Id, desired.Name, desired.Id, otherRankRoles));
            }

            var lines = new List<string>();
            lines.Add($"Mismatches: {deltas.Count}");
            foreach (var d in deltas.Take(20)) lines.Add($"• <@{d.UserId}> → {d.DesiredRoleName}");
            if (deltas.Count > 20) lines.Add($"…and {deltas.Count - 20} more.");
            lines.Add($"Unmatched (not in clan): {unmatchedUsers.Count}");

            var payload = PendingStore.Add(Context.Guild.Id, Context.User.Id, deltas);
            var cb = new ComponentBuilder()
                .WithButton("Apply Changes", customId: $"apply:{payload}", ButtonStyle.Success)
                .WithButton("Cancel", customId: $"cancel:{payload}", ButtonStyle.Danger);

            await FollowupAsync(string.Join("\n", lines), components: cb.Build(), ephemeral: true);
        }

        [SlashCommand("sync_now", "Immediately fetch & apply RS3 rank roles (no prompt)")]
        [DefaultMemberPermissions(GuildPermission.ManageRoles)]
        public async Task SyncNow()
        {
            await DeferAsync(ephemeral: true);
            var cfg = GetCfg();
            var (changed, unmatched) = await _sync.SyncAsync(Context.Guild, cfg.ClanName);
            await FollowupAsync($"✅ Sync complete. Updated {changed} members. Unmatched: {unmatched.Count}", ephemeral: true);
        }

        [SlashCommand("schedule_sync", "Schedule automatic syncs at a fixed hourly interval")]
        [DefaultMemberPermissions(GuildPermission.ManageGuild)]
        public async Task ScheduleSync([Summary(description: "Interval in hours (1-168)")] int interval_hours,
                                       [Summary(description: "Optional channel for summary posts")] ITextChannel? summary_channel = null)
        {
            if (interval_hours < 1 || interval_hours > 168)
            { await RespondAsync("Please choose an interval between 1 and 168 hours.", ephemeral: true); return; }

            var cfg = GetCfg();
            cfg.SummaryChannelId = summary_channel?.Id ?? cfg.SummaryChannelId;

            _sched.Start(Context.Guild.Id, interval_hours, cfg.SummaryChannelId);
            await RespondAsync($"✅ Scheduled sync every **{interval_hours}h**. {(cfg.SummaryChannelId.HasValue ? $"Summaries → <#{cfg.SummaryChannelId.Value}>" : "(no summary channel set)")}", ephemeral: true);
        }

        [SlashCommand("stop_sync", "Stop the scheduled sync for this server")]
        [DefaultMemberPermissions(GuildPermission.ManageGuild)]
        public async Task StopSync()
        {
            _sched.Stop(Context.Guild.Id);
            await RespondAsync("⏹️ Scheduled sync stopped (if any).", ephemeral: true);
        }

        [SlashCommand("ping_unmatched", "Mention members not found in clan roster with a prompt to fix their display names")]
        public async Task PingUnmatched([Summary(description: "Channel to post in (defaults to current)")] ITextChannel? channel = null,
                                        [Summary(description: "Custom message")] string? message = null)
        {
            await DeferAsync(ephemeral: true);
            var cfg = GetCfg();
            if (string.IsNullOrWhiteSpace(cfg.ClanName))
            { await FollowupAsync("❌ No clan connected. Use /clan connect <name> first.", ephemeral: true); return; }

            var roster = await _api.FetchClanAsync(cfg.ClanName);
            if (roster == null)
            { await FollowupAsync("❌ Could not fetch clan members.", ephemeral: true); return; }

            var rsns = roster.Members.Select(m => _norm.Normalize(m.DisplayName)).ToHashSet();
            var unmatched = new List<SocketGuildUser>();
            foreach (var u in Context.Guild.Users)
            {
                if (u.IsBot) continue;
                var rsn = _state.UserRsns.TryGetValue((Context.Guild.Id, u.Id), out var mapped) ? mapped : (u.Nickname ?? u.Username);
                if (!rsns.Contains(_norm.Normalize(rsn))) unmatched.Add(u);
            }

            if (unmatched.Count == 0)
            { await FollowupAsync("✅ Everyone in this server matches the clan roster.", ephemeral: true); return; }

            var target = channel ?? (Context.Channel as ITextChannel);
            if (target == null) { await FollowupAsync("❌ Could not resolve a text channel.", ephemeral: true); return; }

            var prompt = message ?? "If you are mentioned below, please update your **Discord display name** to your exact **RS3 Name** (or ask a mod to `/clan set_rsn` for you) so role syncs work.";

            foreach (var batch in EnumerableBatch.Batch(unmatched.Select(u => u.Mention), 20))
            {
                await target.SendMessageAsync(string.Join(" ", batch));
            }
            await target.SendMessageAsync($"📣 {prompt}");
            await FollowupAsync($"✅ Pinged {unmatched.Count} unmatched member(s) in {MentionUtils.MentionChannel(target.Id)}.", ephemeral: true);
        }

        [ComponentInteraction("apply:*")]
        public async Task Apply(string key)
        {
            if (!PendingStore.TryGet(key, out var entry) || entry.GuildId != Context.Guild.Id || entry.RequestorId != Context.User.Id)
            { await RespondAsync("No pending changes found for you.", ephemeral: true); return; }
            var changed = await _sync.ApplyAsync(Context.Guild, entry.Deltas);
            PendingStore.Remove(key);
            await RespondAsync($"✅ Applied {changed} changes.", ephemeral: true);
        }

        [ComponentInteraction("cancel:*")]
        public async Task Cancel(string key)
        {
            if (!PendingStore.TryGet(key, out var entry) || entry.GuildId != Context.Guild.Id || entry.RequestorId != Context.User.Id)
            { await RespondAsync("No pending action found for you.", ephemeral: true); return; }
            PendingStore.Remove(key);
            await RespondAsync("❎ Cancelled.", ephemeral: true);
        }
    }
}
