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
    public class FileActivityStore : IActivityStore
    {
        private readonly string _root = Path.Combine(AppContext.BaseDirectory, "data", "activity");
        public FileActivityStore()
        {
            Directory.CreateDirectory(_root);
        }

        public async Task SaveSnapshotAsync(ulong guildId, ActivitySnapshot snapshot, CancellationToken ct = default)
        {
            var dir = Path.Combine(_root, guildId.ToString());
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"{snapshot.TakenAt:yyyyMMdd_HHmmss}.json");
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = false });
            await File.WriteAllTextAsync(file, json, ct);
        }

        public async Task<ActivitySnapshot?> LoadLatestSnapshotAsync(ulong guildId, CancellationToken ct = default)
        {
            var dir = Path.Combine(_root, guildId.ToString());
            if (!Directory.Exists(dir)) return null;
            var files = Directory.GetFiles(dir, "*.json").OrderByDescending(f => f).ToList();
            if (files.Count == 0) return null;
            var json = await File.ReadAllTextAsync(files[0], ct);
            return JsonSerializer.Deserialize<ActivitySnapshot>(json);
        }

        public async Task<IReadOnlyList<ActivitySnapshot>> LoadSnapshotsAsync(ulong guildId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
        {
            var dir = Path.Combine(_root, guildId.ToString());
            var list = new List<ActivitySnapshot>();
            if (!Directory.Exists(dir)) return list;
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (DateTimeOffset.TryParseExact(name, "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var ts))
                {
                    if (ts >= start && ts <= end)
                    {
                        var json = await File.ReadAllTextAsync(file, ct);
                        var snap = JsonSerializer.Deserialize<ActivitySnapshot>(json);
                        if (snap != null) list.Add(snap);
                    }
                }
            }
            return list.OrderBy(s => s.TakenAt).ToList();
        }
    }
}
