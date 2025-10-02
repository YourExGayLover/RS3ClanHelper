using System;

namespace RS3ClanHelper.Models
{
    public record GuildConfig
    {
        public string ClanName { get; set; } = string.Empty;
        public ulong? SummaryChannelId { get; set; }

        // Inactive member summary settings
        public ulong? InactiveSummaryChannelId { get; set; }
        public int InactiveDaysThreshold { get; set; } = 30;
        public DayOfWeek InactiveSummaryDay { get; set; } = DayOfWeek.Monday; // Weekly summary day
        public int InactiveSummaryHour { get; set; } = 9; // 24h clock, local server time
    }
}
