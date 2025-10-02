
using System.Collections.Concurrent;

namespace RS3ClanHelper.Features.Events
{
    public record ClanEvent(Guid Id, ulong GuildId, ulong ChannelId, string Title, DateTimeOffset When, List<ulong> Attending);

    public class EventService
    {
        private readonly ConcurrentDictionary<Guid, ClanEvent> _events = new();
        public ClanEvent Create(ulong guildId, ulong channelId, string title, DateTimeOffset when)
        {
            var ev = new ClanEvent(Guid.NewGuid(), guildId, channelId, title, when, new List<ulong>());
            _events[ev.Id] = ev;
            return ev;
        }
        public IEnumerable<ClanEvent> List(ulong guildId) => _events.Values.Where(e=>e.GuildId==guildId).OrderBy(e=>e.When);
        public bool Rsvp(Guid id, ulong userId)
        {
            if (!_events.TryGetValue(id, out var ev)) return false;
            if (!ev.Attending.Contains(userId)) ev.Attending.Add(userId);
            return true;
        }
    }
}
