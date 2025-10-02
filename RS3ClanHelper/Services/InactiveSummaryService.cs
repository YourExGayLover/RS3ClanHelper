using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using RS3ClanHelper.State;

namespace RS3ClanHelper.Services
{
    public class InactiveSummaryService : IInactiveSummaryService
    {
        private readonly AppState _state;
        private readonly IActivityTrackerService _tracker;
        private PeriodicTimer? _timer;
        private readonly TimeSpan _tick = TimeSpan.FromHours(6); // check twice a day

        public InactiveSummaryService(AppState state, IActivityTrackerService tracker)
        {
            _state = state;
            _tracker = tracker;
        }

        public async Task StartAsync(DiscordSocketClient client)
        {
            _timer ??= new PeriodicTimer(_tick);
            _ = Task.Run(async () =>
            {
                while (await _timer.WaitForNextTickAsync())
                {
                    foreach (var g in client.Guilds)
                    {
                        if (!_state.Guilds.TryGetValue(g.Id, out var cfg)) continue;
                        if (cfg.InactiveSummaryChannelId is null) continue;

                        var now = DateTime.Now;
                        if (now.DayOfWeek != cfg.InactiveSummaryDay || now.Hour != cfg.InactiveSummaryHour)
                            continue;

                        // Take a fresh snapshot (so leaderboard and inactivity are current)
                        await _tracker.TakeSnapshotAsync(g);

                        // Compute inactivity
                        var rsnByUser = _state.UserRsns
                            .Where(kv => kv.Key.GuildId == g.Id)
                            .ToDictionary(kv => kv.Key.UserId, kv => kv.Value);

                        var inactive = new System.Collections.Generic.List<(SocketGuildUser user, string rsn)>();

                        foreach (var (userId, rsn) in rsnByUser)
                        {
                            var gain = await _tracker.GetXpGainAsync(g.Id, rsn, cfg.InactiveDaysThreshold);
                            if (gain <= 0)
                            {
                                var u = g.GetUser(userId);
                                if (u != null) inactive.Add((u, rsn));
                            }
                        }

                        if (inactive.Count == 0) continue;
                        var ch = g.GetTextChannel(cfg.InactiveSummaryChannelId.Value);
                        if (ch == null) continue;

                        var embed = new EmbedBuilder()
                            .WithTitle($"Inactive members (≥{cfg.InactiveDaysThreshold} days)")
                            .WithDescription(string.Join("\n", inactive.Take(25).Select(it => $"- {it.user.Mention} ({it.rsn})")))
                            .WithTimestamp(DateTimeOffset.Now)
                            .Build();
                        await ch.SendMessageAsync(embed: embed);
                    }
                }
            });
            await Task.CompletedTask;
        }
    }
}
