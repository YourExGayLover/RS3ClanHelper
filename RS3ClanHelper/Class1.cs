// RS3 Clan Role Sync Discord Bot (Scheduled Syncs + Unmatched Pings)
// .NET 8 single-file example using Discord.Net (Interactions)
// Adds:
//  - /clan schedule_sync <interval_hours> [summary_channel]
//  - /clan stop_sync
//  - /clan ping_unmatched [channel] [message]
//    → Mentions server members not found in the RS3 clan roster asking them to set their display names = RSN
//
// Core features (unchanged):
//  - /clan connect <name> — link this Discord server to an RS3 clan
//  - /clan create_rank_roles — create missing RS rank roles
//  - /clan set_rsn @User RSN — map a Discord user to an RSN (improves matching)
//  - /clan audit_roles — show mismatches & confirm apply
//  - /clan sync_now — immediately apply rank roles
//
// Setup:
// 1) Discord bot with GUILD_MEMBERS intent enabled
// 2) Invite with Manage Roles + Use Slash Commands + Send Messages
// 3) `dotnet add package Discord.Net --version 3.14.1`
// 4) Set DISCORD_TOKEN environment variable
// 5) `dotnet run`
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace RS3ClanHelper;


// ---------------- State / Models ----------------
public class AppState
{
    public readonly Dictionary<ulong, GuildConfig> Guilds = new();
    public readonly Dictionary<(ulong GuildId, ulong UserId), string> UserRsns = new();
    public readonly Dictionary<ulong, SyncJob> SyncJobs = new(); // guildId -> job
}

public record GuildConfig
{
    public string ClanName { get; set; } = string.Empty;
    public ulong? SummaryChannelId { get; set; }
}

public record ClanMember(string DisplayName, string Rank, long ClanXp, long ClanKills);
public record ClanRoster(string ClanName, List<ClanMember> Members);
public record RoleDelta(ulong UserId, string DesiredRoleName, ulong DesiredRoleId, List<ulong> RemoveRoleIds);

public class SyncJob
{
    public int IntervalHours { get; set; }
    public CancellationTokenSource Cts { get; set; } = new();
}

// ---------------- RS3 Helpers ----------------
public static class Rs3
{
    public static readonly string[] RankNames = new[]
    {
        "Owner","Deputy Owner","Overseer","Coordinator","Organiser","Admin",
        "General","Captain","Lieutenant","Sergeant","Corporal","Recruit"
    };

    public static string NormalizeName(string s) => Regex.Replace(s, "\\s+", "").Trim().ToLowerInvariant();

    public static async Task<ClanRoster?> FetchClanAsync(HttpClient http, string clanName)
    {
        try
        {
            var url = $"https://services.runescape.com/m=clan-hiscores/members_lite.ws?clanName={Uri.EscapeDataString(clanName)}";
            var bytes = await http.GetByteArrayAsync(url);
            var text = Encoding.GetEncoding("ISO-8859-1").GetString(bytes); // handle special chars
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var members = new List<ClanMember>();
            foreach (var line in lines)
            {
                var cells = line.Split(',');
                if (cells.Length < 4) continue;
                var name = cells[0].Trim();
                var rank = cells[1].Trim();
                long.TryParse(cells[2], out var xp);
                long.TryParse(cells[3], out var kills);
                members.Add(new ClanMember(name, rank, xp, kills));
            }
            return new ClanRoster(clanName, members);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FetchClanAsync error: {ex.Message}");
            return null;
        }
    }
}

// ---------------- Interaction Module ----------------
[Group("clan", "RS3 clan tools")]
public class ClanModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly AppState _state;
    private readonly HttpClient _http;

    public ClanModule(AppState state, HttpClient http) { _state = state; _http = http; }

    private GuildConfig GetCfg()
    {
        if (!_state.Guilds.TryGetValue(Context.Guild.Id, out var cfg))
            _state.Guilds[Context.Guild.Id] = cfg = new GuildConfig();
        return cfg;
    }

    // ---- Core binding ----
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
        foreach (var rn in Rs3.RankNames)
        {
            var role = Context.Guild.Roles.FirstOrDefault(r => r.Name.Equals(rn, StringComparison.OrdinalIgnoreCase));
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

    // ---- Audit & Sync ----
    [SlashCommand("audit_roles", "Compare Discord roles against RS3 clan ranks")]
    public async Task AuditRoles()
    {
        await DeferAsync(ephemeral: true);
        var cfg = GetCfg();
        if (string.IsNullOrWhiteSpace(cfg.ClanName))
        { await FollowupAsync("❌ No clan connected. Use /clan connect <name> first.", ephemeral: true); return; }

        var clan = await Rs3.FetchClanAsync(_http, cfg.ClanName);
        if (clan == null)
        { await FollowupAsync($"❌ Could not fetch clan members for **{cfg.ClanName}**.", ephemeral: true); return; }

        var rsnToRank = clan.Members
            .GroupBy(m => Rs3.NormalizeName(m.DisplayName))
            .ToDictionary(g => g.Key, g => g.First().Rank);

        var deltas = new List<RoleDelta>();
        var unmatchedUsers = new List<SocketGuildUser>();

        foreach (var user in Context.Guild.Users)
        {
            if (user.IsBot) continue;
            var rsn = _state.UserRsns.TryGetValue((Context.Guild.Id, user.Id), out var mapped)
                ? mapped
                : (user.Nickname ?? user.Username);

            if (!rsnToRank.TryGetValue(Rs3.NormalizeName(rsn), out var rank))
            { unmatchedUsers.Add(user); continue; }

            var desired = Context.Guild.Roles.FirstOrDefault(r => r.Name.Equals(rank, StringComparison.OrdinalIgnoreCase));
            if (desired == null) continue;

            var hasDesired = user.Roles.Any(r => r.Id == desired.Id);
            var otherRankRoles = user.Roles.Where(r => Rs3.RankNames.Any(n => n.Equals(r.Name, StringComparison.OrdinalIgnoreCase)) && r.Id != desired.Id).Select(r => r.Id).ToList();
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
        var (changed, unmatched) = await DoSync(Context.Guild);
        await FollowupAsync($"✅ Sync complete. Updated {changed} members. Unmatched: {unmatched.Count}", ephemeral: true);
    }

    private async Task<(int changed, List<SocketGuildUser> unmatched)> DoSync(SocketGuild guild)
    {
        var cfg = GetCfg();
        var unmatchedUsers = new List<SocketGuildUser>();
        if (string.IsNullOrWhiteSpace(cfg.ClanName)) return (0, unmatchedUsers);

        var clan = await Rs3.FetchClanAsync(_http, cfg.ClanName);
        if (clan == null) return (0, unmatchedUsers);
        var rsnToRank = clan.Members.ToDictionary(m => Rs3.NormalizeName(m.DisplayName), m => m.Rank);

        int changed = 0;
        foreach (var user in guild.Users)
        {
            if (user.IsBot) continue;
            var rsn = _state.UserRsns.TryGetValue((guild.Id, user.Id), out var mapped) ? mapped : (user.Nickname ?? user.Username);
            if (!rsnToRank.TryGetValue(Rs3.NormalizeName(rsn), out var rank)) { unmatchedUsers.Add(user); continue; }
            var desired = guild.Roles.FirstOrDefault(r => r.Name.Equals(rank, StringComparison.OrdinalIgnoreCase));
            if (desired == null) continue;
            var toRemove = user.Roles.Where(r => Rs3.RankNames.Any(n => n.Equals(r.Name, StringComparison.OrdinalIgnoreCase)) && r.Id != desired.Id).ToList();
            if (!user.Roles.Any(r => r.Id == desired.Id) || toRemove.Count > 0)
            {
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
                    Console.WriteLine($"Role change failed for {user.Username}: {ex.Message}");
                }
            }
        }
        return (changed, unmatchedUsers);
    }

    // ---- Scheduled Syncs ----
    [SlashCommand("schedule_sync", "Schedule automatic syncs at a fixed hourly interval")]
    [DefaultMemberPermissions(GuildPermission.ManageGuild)]
    public async Task ScheduleSync([Summary(description: "Interval in hours (1-168)")] int interval_hours,
                                   [Summary(description: "Optional channel for summary posts")] ITextChannel? summary_channel = null)
    {
        if (interval_hours < 1 || interval_hours > 168)
        { await RespondAsync("Please choose an interval between 1 and 168 hours.", ephemeral: true); return; }

        var cfg = GetCfg();
        cfg.SummaryChannelId = summary_channel?.Id ?? cfg.SummaryChannelId;

        // Stop any existing job
        if (_state.SyncJobs.TryGetValue(Context.Guild.Id, out var existing))
        {
            existing.Cts.Cancel();
            _state.SyncJobs.Remove(Context.Guild.Id);
        }

        var cts = new CancellationTokenSource();
        var job = new SyncJob { IntervalHours = interval_hours, Cts = cts };
        _state.SyncJobs[Context.Guild.Id] = job;

        _ = Task.Run(async () =>
        {
            var token = cts.Token;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var guild = Context.Guild; // captured reference stays valid
                    var (changed, unmatched) = await DoSync(guild);

                    if (cfg.SummaryChannelId.HasValue)
                    {
                        var chan = guild.GetTextChannel(cfg.SummaryChannelId.Value);
                        if (chan != null)
                        {
                            await chan.SendMessageAsync($"🔄 Scheduled sync ran: updated **{changed}** members. Unmatched: **{unmatched.Count}**.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Scheduled sync error: {ex}");
                }
                // Wait for the interval or cancellation
                try { await Task.Delay(TimeSpan.FromHours(interval_hours), token); } catch { }
            }
        });

        await RespondAsync($"✅ Scheduled sync every **{interval_hours}h**. {(cfg.SummaryChannelId.HasValue ? $"Summaries → <#{cfg.SummaryChannelId.Value}>" : "(no summary channel set)")}", ephemeral: true);
    }

    [SlashCommand("stop_sync", "Stop the scheduled sync for this server")]
    [DefaultMemberPermissions(GuildPermission.ManageGuild)]
    public async Task StopSync()
    {
        if (_state.SyncJobs.TryGetValue(Context.Guild.Id, out var job))
        {
            job.Cts.Cancel();
            _state.SyncJobs.Remove(Context.Guild.Id);
            await RespondAsync("⏹️ Scheduled sync stopped.", ephemeral: true);
        }
        else
        {
            await RespondAsync("No scheduled sync is running.", ephemeral: true);
        }
    }

    // ---- Ping Unmatched ----
    [SlashCommand("ping_unmatched", "Mention members not found in clan roster with a prompt to fix their display names")]
    public async Task PingUnmatched([Summary(description: "Channel to post in (defaults to current)")] ITextChannel? channel = null,
                                    [Summary(description: "Custom message")] string? message = null)
    {
        await DeferAsync(ephemeral: true);
        var cfg = GetCfg();
        if (string.IsNullOrWhiteSpace(cfg.ClanName))
        { await FollowupAsync("❌ No clan connected. Use /clan connect <name> first.", ephemeral: true); return; }

        var clan = await Rs3.FetchClanAsync(_http, cfg.ClanName);
        if (clan == null)
        { await FollowupAsync("❌ Could not fetch clan members.", ephemeral: true); return; }

        var rsns = clan.Members.Select(m => Rs3.NormalizeName(m.DisplayName)).ToHashSet();
        var unmatched = new List<SocketGuildUser>();
        foreach (var u in Context.Guild.Users)
        {
            if (u.IsBot) continue;
            var rsn = _state.UserRsns.TryGetValue((Context.Guild.Id, u.Id), out var mapped) ? mapped : (u.Nickname ?? u.Username);
            if (!rsns.Contains(Rs3.NormalizeName(rsn))) unmatched.Add(u);
        }

        if (unmatched.Count == 0)
        { await FollowupAsync("✅ Everyone in this server matches the clan roster.", ephemeral: true); return; }

        var target = channel ?? (Context.Channel as ITextChannel);
        if (target == null) { await FollowupAsync("❌ Could not resolve a text channel.", ephemeral: true); return; }

        var prompt = message ?? "If you are mentioned below, please update your **Discord display name** to your exact **RS3 Name** (or ask a mod to `/clan set_rsn` for you) so role syncs work.";

        // Build mentions in batches to avoid overly long messages
        var batches = Batch(unmatched.Select(u => u.Mention), 20);
        await target.SendMessageAsync($"📣 {prompt}");
        foreach (var batch in batches)
        {
            await target.SendMessageAsync(string.Join(" ", batch));
        }

        await FollowupAsync($"✅ Pinged {unmatched.Count} unmatched member(s) in {MentionUtils.MentionChannel(target.Id)}.", ephemeral: true);
    }

    private static IEnumerable<IEnumerable<string>> Batch(IEnumerable<string> items, int size)
    {
        var list = new List<string>(size);
        foreach (var it in items)
        {
            list.Add(it);
            if (list.Count == size)
            {
                yield return list.ToArray();
                list.Clear();
            }
        }
        if (list.Count > 0) yield return list.ToArray();
    }

    // ---- Button handlers ----
    [ComponentInteraction("apply:*")]
    public async Task Apply(string key)
    {
        if (!PendingStore.TryGet(key, out var entry) || entry.GuildId != Context.Guild.Id || entry.RequestorId != Context.User.Id)
        { await RespondAsync("No pending changes found for you.", ephemeral: true); return; }
        int changed = 0;
        foreach (var d in entry.Deltas)
        {
            var user = Context.Guild.GetUser(d.UserId);
            if (user == null) continue;
            var desired = Context.Guild.GetRole(d.DesiredRoleId);
            var toRemove = d.RemoveRoleIds.Select(Context.Guild.GetRole).Where(r => r != null)!.Cast<SocketRole>().ToList();
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
                Console.WriteLine($"Apply button error for {user?.Username}: {ex.Message}");
            }
        }
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

// Simple in-memory store for pending apply prompts
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

    public class PendingEntry
    {
        public ulong GuildId { get; set; }
        public ulong RequestorId { get; set; }
        public List<RoleDelta> Deltas { get; set; } = new();
    }
}
