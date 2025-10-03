using System.Threading.Tasks;
using Discord.WebSocket;

namespace RS3ClanHelper.Services
{
    public interface IEventReminderService
    {
        Task StartAsync(DiscordSocketClient client);
    }
}
