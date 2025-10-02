
namespace RS3ClanHelper.Models
{
    public record HiscoreSnapshot(string Rsn, long TotalXp, DateTime Timestamp);
    public record XpGain(string Rsn, long Gain, DateTime Since);
}
