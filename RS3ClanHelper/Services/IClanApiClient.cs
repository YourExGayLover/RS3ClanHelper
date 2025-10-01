using System.Threading;
using System.Threading.Tasks;
using RS3ClanHelper.Models;

namespace RS3ClanHelper.Services
{
    public interface IClanApiClient
    {
        Task<ClanRoster?> FetchClanAsync(string clanName, CancellationToken ct = default);
    }
}
