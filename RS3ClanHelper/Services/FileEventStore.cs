using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RS3ClanHelper.Models;

namespace RS3ClanHelper.Services
{
    public class FileEventStore : IEventStore
    {
        private readonly string _root = Path.Combine(AppContext.BaseDirectory, "data", "events");
        public FileEventStore() { Directory.CreateDirectory(_root); }

        private string Dir(ulong guildId) => Path.Combine(_root, guildId.ToString());
        private string PathFor(ulong guildId, string id) => Path.Combine(Dir(guildId), id + ".json");

        public async Task SaveAsync(ClanEvent evt, CancellationToken ct = default)
        {
            Directory.CreateDirectory(Dir(evt.GuildId));
            var json = JsonSerializer.Serialize(evt, new JsonSerializerOptions{ WriteIndented = true });
            await File.WriteAllTextAsync(PathFor(evt.GuildId, evt.Id), json, ct);
        }

        public async Task<ClanEvent?> LoadAsync(ulong guildId, string id, CancellationToken ct = default)
        {
            var p = PathFor(guildId, id);
            if (!File.Exists(p)) return null;
            return JsonSerializer.Deserialize<ClanEvent>(await File.ReadAllTextAsync(p, ct));
        }

        public async Task<IReadOnlyList<ClanEvent>> ListUpcomingAsync(ulong guildId, CancellationToken ct = default)
        {
            var d = Dir(guildId);
            if (!Directory.Exists(d)) return Array.Empty<ClanEvent>();
            var list = new List<ClanEvent>();
            foreach (var file in Directory.GetFiles(d, "*.json"))
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var evt = JsonSerializer.Deserialize<ClanEvent>(json);
                if (evt != null && evt.StartsAt > DateTimeOffset.UtcNow.AddDays(-1))
                    list.Add(evt);
            }
            return list.OrderBy(e => e.StartsAt).ToList();
        }

        public Task DeleteAsync(ulong guildId, string id, CancellationToken ct = default)
        {
            var p = PathFor(guildId, id);
            if (File.Exists(p)) File.Delete(p);
            return Task.CompletedTask;
        }
    }
}
