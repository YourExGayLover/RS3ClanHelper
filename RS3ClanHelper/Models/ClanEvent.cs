using System;
using System.Collections.Generic;

namespace RS3ClanHelper.Models
{
    public class ClanEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("n");
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong? MessageId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTimeOffset StartsAt { get; set; }

        // Existing reminder flags
        public bool Reminded24h { get; set; }
        public bool Reminded1h { get; set; }

        // NEW reminder flags
        public bool Reminded15m { get; set; }
        public bool RemindedStart { get; set; }

        // NEW: link to native Discord Scheduled Event (if created)
        public ulong? DiscordScheduledEventId { get; set; }

        public HashSet<ulong> Yes { get; set; } = new();
        public HashSet<ulong> Maybe { get; set; } = new();
        public HashSet<ulong> No { get; set; } = new();
    }
}
