using System.Threading;
using System.Threading.Tasks;

namespace RS3ClanHelper.Services
{
    public interface IHiscoreClient
    {
        // Returns total XP from RS3 hiscores (or null if not found)
        Task<long?> GetTotalXpAsync(string rsn, CancellationToken ct = default);

        // Returns hiscore "overall rank" if available
        Task<long?> GetOverallRankAsync(string rsn, CancellationToken ct = default);
    }
}
