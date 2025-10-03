using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;

namespace RS3ClanHelper.Modules
{
    [Group("events", "Manage native Discord Scheduled Events")]
    public class DiscordEventsModule : InteractionModuleBase<SocketInteractionContext>
    {
        // /events create "<title>" "<start>" [end] [location] [description]
        [SlashCommand("create", "Create a native Discord Scheduled Event (External)")]
        [DefaultMemberPermissions(GuildPermission.ManageEvents)]
        public async Task Create(
            [Summary(description: "Title of the event")] string title,
            [Summary(description: "Start date/time (e.g., '2025-10-05 19:00' or 'Oct 5 7pm')")] string start_time,
            [Summary(description: "Optional end date/time (default +2h)")] string? end_time = null,
            [Summary(description: "External location label (e.g., 'Clan PvM')")] string? location = null,
            [Summary(description: "Description shown on the Discord event")] string? description = null
        )
        {
            await DeferAsync(ephemeral: true);

            if (!TryParseDate(start_time, out var start))
            {
                await FollowupAsync("Could not parse **start** time. Try formats like `2025-10-05 19:00`, `Oct 5 7pm`, or `MM/dd/yyyy HH:mm`.", ephemeral: true);
                return;
            }

            DateTimeOffset end;
            if (!string.IsNullOrWhiteSpace(end_time) && TryParseDate(end_time!, out var parsedEnd))
            {
                end = parsedEnd.ToUniversalTime();
                if (end <= start) end = start.AddHours(2);
            }
            else
            {
                end = start.AddHours(2);
            }

            var me = Context.Guild.CurrentUser;
            if (!me.GuildPermissions.ManageEvents)
            {
                await FollowupAsync(":warning: I don't have **Manage Events** permission. Ask an admin to enable it for my role.", ephemeral: true);
                return;
            }

            string loc = string.IsNullOrWhiteSpace(location) ? "Clan Event" : location!.Trim();
            string desc = string.IsNullOrWhiteSpace(description) ? "" : description!.Trim();

            // Create an External scheduled event
            var created = await Context.Guild.CreateEventAsync(
                name: title,
                type: GuildScheduledEventType.External,
                startTime: start,
                endTime: end,
                location: loc,
                description: desc
            );

            await FollowupAsync($":calendar_spiral: **Discord Event created**: **{created.Name}** " +
                                $"<t:{created.StartTime.ToUnixTimeSeconds()}:F> — https://discord.com/events/{Context.Guild.Id}/{created.Id}", ephemeral: false);
        }

        // /events list  — show upcoming scheduled events
        [SlashCommand("list", "List upcoming Discord Scheduled Events in this server")]
        public async Task List(int days = 60)
        {
            await DeferAsync(ephemeral: true);

            var all = await Context.Guild.GetEventsAsync();
            var now = DateTimeOffset.UtcNow;
            var until = now.AddDays(Math.Clamp(days, 1, 180));
            var upcoming = all
                .Where(e => e.StartTime <= until && e.EndTime >= now || e.StartTime >= now)
                .OrderBy(e => e.StartTime)
                .ToList();

            if (upcoming.Count == 0)
            {
                await FollowupAsync("No upcoming events found.", ephemeral: true);
                return;
            }

            var eb = new EmbedBuilder()
                .WithTitle($":calendar: Upcoming Events (next {Math.Clamp(days,1,180)}d)")
                .WithColor(Color.Blue)
                .WithCurrentTimestamp();

            foreach (var e in upcoming.Take(10))
            {
                var url = $"https://discord.com/events/{Context.Guild.Id}/{e.Id}";
                var line = $"• **{e.Name}** — <t:{e.StartTime.ToUnixTimeSeconds()}:F> (<t:{e.StartTime.ToUnixTimeSeconds()}:R>) — {url}";
                eb.AddField("\u200B", line, false);
            }

            await FollowupAsync(embed: eb.Build(), ephemeral: true);
        }

        // /events cancel <event_id>
        [SlashCommand("cancel", "Cancel a Discord Scheduled Event by its ID")]
        [DefaultMemberPermissions(GuildPermission.ManageEvents)]
        public async Task Cancel(ulong event_id)
        {
            await DeferAsync(ephemeral: true);

            var me = Context.Guild.CurrentUser;
            if (!me.GuildPermissions.ManageEvents)
            {
                await FollowupAsync(":warning: I don't have **Manage Events** permission.", ephemeral: true);
                return;
            }

            var e = await Context.Guild.GetEventAsync(event_id);
            if (e == null)
            {
                await FollowupAsync(":mag: Event not found. Double-check the ID (you can get it from `/events list`).", ephemeral: true);
                return;
            }

            await e.DeleteAsync();
            await FollowupAsync($":wastebasket: Deleted **{e.Name}** ({event_id}).", ephemeral: true);
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
