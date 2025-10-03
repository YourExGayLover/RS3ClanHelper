using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Discord;
using Discord.WebSocket;
using RS3ClanHelper.Models;

namespace RS3ClanHelper.Services
{
    public class EventReminderService : IEventReminderService
    {
        private readonly IEventStore _store;
        private PeriodicTimer? _timer;
        private readonly TimeSpan _tick = TimeSpan.FromMinutes(1);

        public EventReminderService(IEventStore store) { _store = store; }

        public async Task StartAsync(DiscordSocketClient client)
        {
            _timer ??= new PeriodicTimer(_tick);
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                while (await _timer.WaitForNextTickAsync())
                {
                    foreach (var g in client.Guilds)
                    {
                        var events = await _store.ListUpcomingAsync(g.Id);
                        foreach (var evt in events)
                        {
                            try
                            {
                                var minutesUntil = (evt.StartsAt - DateTimeOffset.UtcNow).TotalMinutes;
                                var ch = g.GetTextChannel(evt.ChannelId);
                                if (ch == null) continue;

                                // 24h reminder
                                if (!evt.Reminded24h && minutesUntil <= 24*60 && minutesUntil > 24*60 - 2)
                                {
                                    await ch.SendMessageAsync($":alarm_clock: **24h Reminder:** **{evt.Title}** starts <t:{evt.StartsAt.ToUnixTimeSeconds()}:R>.");
                                    evt.Reminded24h = true;
                                    await _store.SaveAsync(evt);
                                }
                                // 1h reminder
                                if (!evt.Reminded1h && minutesUntil <= 60 && minutesUntil > 58)
                                {
                                    await ch.SendMessageAsync($":alarm_clock: **1h Reminder:** **{evt.Title}** starts <t:{evt.StartsAt.ToUnixTimeSeconds()}:R>. RSVP if you haven't!");
                                    evt.Reminded1h = true;
                                    await _store.SaveAsync(evt);
                                }
                                // NEW: 15m reminder
                                if (!evt.Reminded15m && minutesUntil <= 15 && minutesUntil > 13)
                                {
                                    await ch.SendMessageAsync($":alarm_clock: **15m Reminder:** **{evt.Title}** starts <t:{evt.StartsAt.ToUnixTimeSeconds()}:R>. Get ready!");
                                    evt.Reminded15m = true;
                                    await _store.SaveAsync(evt);
                                }
                                // NEW: start announcement (0m)
                                if (!evt.RemindedStart && minutesUntil <= 0 && minutesUntil > -2)
                                {
                                    await ch.SendMessageAsync($":tada: **{evt.Title}** is starting now!");
                                    evt.RemindedStart = true;
                                    await _store.SaveAsync(evt);
                                }
                            }
                            catch { /* keep loop alive */ }
                        }
                    }
                }
            });
            await System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
