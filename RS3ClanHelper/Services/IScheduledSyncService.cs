namespace RS3ClanHelper.Services
{
    public interface IScheduledSyncService
    {
        void Start(ulong guildId, int hours, ulong? summaryChannelId);
        void Stop(ulong guildId);
    }
}
