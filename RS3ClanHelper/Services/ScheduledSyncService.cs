using System;
using System.Threading;
using Discord.WebSocket;
using RS3ClanHelper.State;
using RS3ClanHelper.Models;

namespace RS3ClanHelper.Services
{
    public class ScheduledSyncService : IScheduledSyncService
    {
        private readonly AppState _state;
        private readonly IRoleSyncService _sync;
        private readonly DiscordSocketClient _client;

        public ScheduledSyncService(AppState state, IRoleSyncService sync, DiscordSocketClient client)
        {
            _state = state; _sync = sync; _client = client;
        }

        public void Start(ulong guildId, int hours, ulong? summaryChannelId)
        {
            if (_state.SyncJobs.TryGetValue(guildId, out var existing))
            {
                existing.Cts.Cancel();
                _state.SyncJobs.Remove(guildId);
            }

            var job = new SyncJob { IntervalHours = hours, Cts = new CancellationTokenSource() };
            _state.SyncJobs[guildId] = job;

            _ = Task.Run(async () =>
            {
                var token = job.Cts.Token;
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var guild = _client.GetGuild(guildId);
                        if (guild != null && _state.Guilds.TryGetValue(guildId, out var cfg) && !string.IsNullOrWhiteSpace(cfg.ClanName))
                        {
                            var (changed, unmatched) = await _sync.SyncAsync(guild, cfg.ClanName);
                            if (summaryChannelId.HasValue)
                            {
                                var chan = guild.GetTextChannel(summaryChannelId.Value);
                                if (chan != null)
                                    await chan.SendMessageAsync($"🔄 Scheduled sync ran: updated **{changed}** members. Unmatched: **{unmatched.Count}**.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Scheduled sync error: {ex}");
                    }
                    try { await Task.Delay(TimeSpan.FromHours(hours), token); } catch { }
                }
            });
        }

        public void Stop(ulong guildId)
        {
            if (_state.SyncJobs.TryGetValue(guildId, out var job))
            {
                job.Cts.Cancel();
                _state.SyncJobs.Remove(guildId);
            }
        }
    }
}
