
using System.Text.Json;
using RS3ClanHelper.Models;

namespace RS3ClanHelper.Services
{
    public class StorageService
    {
        private readonly string _baseDir;
        private readonly JsonSerializerOptions _opts = new() { WriteIndented = true };
        public StorageService()
        {
            _baseDir = Path.Combine(AppContext.BaseDirectory, "data");
            Directory.CreateDirectory(_baseDir);
        }
        public T Load<T>(string name) where T : new()
        {
            var p = Path.Combine(_baseDir, name);
            if (!File.Exists(p)) return new T();
            var json = File.ReadAllText(p);
            return JsonSerializer.Deserialize<T>(json) ?? new T();
        }
        public void Save<T>(string name, T value)
        {
            var p = Path.Combine(_baseDir, name);
            File.WriteAllText(p, JsonSerializer.Serialize(value, _opts));
        }
        public string PathJoin(params string[] parts) => Path.Combine(_baseDir, Path.Combine(parts));
    }
}
