
namespace RS3ClanHelper.Models
{
    public class BotConfig
    {
        public string ClanName { get; set; } = string.Empty;
        public ulong? LogsChannelId { get; set; }
        public ulong? WelcomeChannelId { get; set; }
        public bool AutoNicknameSync { get; set; } = false;
        public bool AutoRoleSyncOnJoin { get; set; } = true;
        public int SnapshotIntervalMinutes { get; set; } = 60;
        public int InactiveDaysThreshold { get; set; } = 14;
        public Dictionary<string, ulong> RankRoleMap { get; set; } = new();
    }
}
