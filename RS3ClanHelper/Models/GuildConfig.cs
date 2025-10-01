namespace RS3ClanHelper.Models
{
    public record GuildConfig
    {
        public string ClanName { get; set; } = string.Empty;
        public ulong? SummaryChannelId { get; set; }
    }
}
