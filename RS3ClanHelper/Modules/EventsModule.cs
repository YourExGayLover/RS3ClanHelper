using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using RS3ClanHelper.Models;
using RS3ClanHelper.Services;

namespace RS3ClanHelper.Modules
{
    // Top-level group: /events
    [Group("events", "Clan events")]
    public class EventsModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IEventStore _store;
        public EventsModule(IEventStore store) { _store = store; }

        // /events create <title> <start_time> [channel]
        [SlashCommand("create", "Create a clan event and post RSVP buttons")]
        [DefaultMemberPermissions(GuildPermission.ManageGuild)]
        public async Task Create(
            [Summary(description: "Title of the event")] string title,
            [Summary(description: "Start date/time (e.g., '2025-10-05 19:00')")] string start_time,
            [Summary(description: "Channel to post in (default: current)")] ITextChannel? channel = null)
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

            var embed = BuildEmbed(evt);
            var row = new ComponentBuilder()
                .WithButton("RSVP ✅", $"evt:rsvp:{evt.Id}:yes", ButtonStyle.Success)
                .WithButton("Maybe ❓", $"evt:rsvp:{evt.Id}:maybe", ButtonStyle.Primary)
                .WithButton("Decline ❌", $"evt:rsvp:{evt.Id}:no", ButtonStyle.Danger)
                .Build();

            var msg = await ch.SendMessageAsync(embed: embed, components: row);
            evt.MessageId = msg.Id;
            await _store.SaveAsync(evt);

            await FollowupAsync($":calendar_spiral: Event created: **{evt.Title}** at <t:{evt.StartsAt.ToUnixTimeSeconds()}:F> in {ch.Mention}", ephemeral: true);
        }

        [ComponentInteraction("evt:rsvp:*:*", ignoreGroupNames: true)]
        public async Task RsvpHandler(string eventId, string choice)
        {
            // IMPORTANT: acknowledge within 3s
            await DeferAsync(ephemeral: true);

            try
            {
                var evt = await _store.LoadAsync(Context.Guild.Id, eventId);
                if (evt == null)
                {
                    await FollowupAsync("Event not found or expired.", ephemeral: true);
                    return;
                }

                // single state per user
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

                // best-effort: refresh embed counts
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
                        catch { /* keep interaction healthy */ }
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
            return new EmbedBuilder()
                .WithTitle($"📅 {evt.Title}")
                .WithDescription($"Starts: <t:{evt.StartsAt.ToUnixTimeSeconds()}:F> (<t:{evt.StartsAt.ToUnixTimeSeconds()}:R>)")
                .AddField("Yes", evt.Yes.Count.ToString(), true)
                .AddField("Maybe", evt.Maybe.Count.ToString(), true)
                .AddField("No", evt.No.Count.ToString(), true)
                .WithFooter($"Event ID: {evt.Id}")
                .WithCurrentTimestamp()
                .Build();
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
