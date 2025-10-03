using System;
using System.Globalization;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using RS3ClanHelper.Models;
using RS3ClanHelper.Services;

namespace RS3ClanHelper.Modules
{
    [Group("events", "Clan events")]
    public class EventsModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IEventStore _store;
        public EventsModule(IEventStore store) { _store = store; }

        [SlashCommand("create", "Create a clan event (embed+RSVP) and a Discord scheduled event")]
        [DefaultMemberPermissions(GuildPermission.ManageGuild | GuildPermission.ManageEvents)]
        public async Task Create(
            [Summary(description: "Title of the event")] string title,
            [Summary(description: "Start date/time (e.g., '2025-10-05 19:00')")] string start_time,
            [Summary(description: "Channel to post in (default: current)")] ITextChannel? channel = null,
            [Summary(description: "Optional description for the Discord event")] string? description = null,
            [Summary(description: "Optional end time (e.g., '2025-10-05 21:00')")] string? end_time = null
        )
        {
            await DeferAsync(ephemeral: true);

            if (!TryParseDate(start_time, out var when))
            {
                await FollowupAsync("Could not parse date/time. Try formats like `2025-10-05 19:00` or `Oct 5 7pm`.", ephemeral: true);
                return;
            }
            var ch = channel ?? (ITextChannel)Context.Channel;
            var evt = new ClanEvent
            {
                GuildId = Context.Guild.Id,
                ChannelId = ch.Id,
                Title = title.Trim(),
                StartsAt = when.ToUniversalTime()
            };

            // Build RSVP embed + buttons
            var embed = BuildEmbed(evt);
            var row = new ComponentBuilder()
                .WithButton("RSVP ✅", $"evt:rsvp:{evt.Id}:yes", ButtonStyle.Success)
                .WithButton("Maybe ❓", $"evt:rsvp:{evt.Id}:maybe", ButtonStyle.Primary)
                .WithButton("Decline ❌", $"evt:rsvp:{evt.Id}:no", ButtonStyle.Danger)
                .Build();

            var msg = await ch.SendMessageAsync(embed: embed, components: row);
            evt.MessageId = msg.Id;

            // ---- Create native Discord Scheduled Event (External) (if the bot has Manage Events) ----
            var start = evt.StartsAt;
            DateTimeOffset end;
            if (!string.IsNullOrWhiteSpace(end_time) && TryParseDate(end_time, out var parsedEnd))
            {
                end = parsedEnd.ToUniversalTime();
                if (end <= start) end = start.AddHours(2);
            }
            else
            {
                end = start.AddHours(2);
            }

            string eventDescription = string.IsNullOrWhiteSpace(description) ? "" : description.Trim();
            var messageLink = $"https://discord.com/channels/{Context.Guild.Id}/{ch.Id}/{msg.Id}";
            if (!string.IsNullOrWhiteSpace(eventDescription))
                eventDescription += "\n\n";
            eventDescription += $"RSVP & details: {messageLink}";

            // Only attempt creating the Discord Scheduled Event if the bot has Manage Events
            var me = Context.Guild.CurrentUser;
            if (me.GuildPermissions.ManageEvents)
            {
                try
                {
                    var discordEvt = await Context.Guild.CreateEventAsync(
                        name: evt.Title,
                        type: GuildScheduledEventType.External,
                        startTime: start,
                        endTime: end,
                        location: ch.Name,
                        description: eventDescription
                    );

                    evt.DiscordScheduledEventId = discordEvt.Id;
                }
                catch (Exception ex)
                {
                    // Log and continue; we still have the RSVP embed
                    Console.WriteLine($"[Events] Failed to create Discord Scheduled Event: {ex}");
                }
            }
            else
            {
                // Inform the invoker but continue; RSVP embed is already posted
                await FollowupAsync(
                    ":warning: I don’t have **Manage Events** permission, so I skipped creating a Discord Scheduled Event.\n" +
                    "An admin can enable **Manage Events** on my bot role to allow this.",
                    ephemeral: true
                );
            }

            // Save now that we have message + (optional) discord event id
            await _store.SaveAsync(evt);

            // Update message embed to include link to Discord Event (if created)
            try
            {
                if (await ch.GetMessageAsync(msg.Id) is IUserMessage original)
                {
                    var updated = BuildEmbed(evt);
                    await original.ModifyAsync(m => m.Embed = updated);
                }
            }
            catch { /* non-fatal */ }

            // Final confirmation (include link only if the native event was created)
            var extra = evt.DiscordScheduledEventId.HasValue
                ? $"\n:link: Discord Event: https://discord.com/events/{Context.Guild.Id}/{evt.DiscordScheduledEventId.Value}"
                : string.Empty;

            await FollowupAsync(
                $":calendar_spiral: Event created: **{evt.Title}** at <t:{evt.StartsAt.ToUnixTimeSeconds()}:F> in {ch.Mention}{extra}",
                ephemeral: true
            );

        }

        [ComponentInteraction("evt:rsvp:*:*", ignoreGroupNames: true)]
        public async Task RsvpHandler(string eventId, string choice)
        {
            await DeferAsync(ephemeral: true);
            try
            {
                var evt = await _store.LoadAsync(Context.Guild.Id, eventId);
                if (evt == null)
                {
                    await FollowupAsync("Event not found or expired.", ephemeral: true);
                    return;
                }

                evt.Yes.Remove(Context.User.Id);
                evt.Maybe.Remove(Context.User.Id);
                evt.No.Remove(Context.User.Id);
                switch (choice)
                {
                    case "yes": evt.Yes.Add(Context.User.Id); break;
                    case "maybe": evt.Maybe.Add(Context.User.Id); break;
                    case "no": evt.No.Add(Context.User.Id); break;
                }
                await _store.SaveAsync(evt);

                if (evt.MessageId is ulong mid)
                {
                    var ch = Context.Guild.GetTextChannel(evt.ChannelId);
                    if (ch != null)
                    {
                        try
                        {
                            if (await ch.GetMessageAsync(mid) is IUserMessage msg)
                                await msg.ModifyAsync(m => m.Embed = BuildEmbed(evt));
                        }
                        catch { }
                    }
                }

                await FollowupAsync("RSVP recorded ✔️", ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($":warning: Could not record RSVP. {ex.Message}", ephemeral: true);
            }
        }

        private static Embed BuildEmbed(ClanEvent evt)
        {
            var eb = new EmbedBuilder()
                .WithTitle($"📅 {evt.Title}")
                .WithDescription($"Starts: <t:{evt.StartsAt.ToUnixTimeSeconds()}:F> (<t:{evt.StartsAt.ToUnixTimeSeconds()}:R>)")
                .AddField("Yes", evt.Yes.Count.ToString(), true)
                .AddField("Maybe", evt.Maybe.Count.ToString(), true)
                .AddField("No", evt.No.Count.ToString(), true)
                .WithFooter($"Event ID: {evt.Id}")
                .WithCurrentTimestamp();

            if (evt.DiscordScheduledEventId.HasValue)
            {
                var url = $"https://discord.com/events/{evt.GuildId}/{evt.DiscordScheduledEventId.Value}";
                eb.AddField("Discord Event", url, false);
            }

            return eb.Build();
        }

        private static bool TryParseDate(string input, out DateTimeOffset dto)
        {
            input = input.Trim();
            string[] fmts = new[] {
                "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd HH:mm",
                "MM/dd/yyyy HH:mm", "dd/MM/yyyy HH:mm",
                "MMM d yyyy h:mm tt", "MMM d h:mm tt",
                "MMMM d yyyy h:mm tt", "MMMM d h:mm tt"
            };
            foreach (var f in fmts)
                if (DateTimeOffset.TryParseExact(input, f, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dto))
                    return true;
            return DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dto);
        }
    }
}
