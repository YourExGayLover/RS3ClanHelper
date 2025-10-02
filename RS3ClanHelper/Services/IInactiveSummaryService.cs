using System.Threading.Tasks;
using Discord.WebSocket;

namespace RS3ClanHelper.Services
{
    public interface IInactiveSummaryService
    {
        Task StartAsync(DiscordSocketClient client);
    }
}
