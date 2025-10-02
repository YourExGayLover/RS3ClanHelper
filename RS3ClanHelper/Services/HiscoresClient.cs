
using System.Net.Http;
using System.Threading.Tasks;
using System.Globalization;

namespace RS3ClanHelper.Services
{
    public class HiscoresClient
    {
        private readonly HttpClient _http;
        public HiscoresClient(HttpClient http) { _http = http; }

        public async Task<long?> GetTotalXpAsync(string rsn, CancellationToken ct = default)
        {
            try
            {
                // RuneScape hiscores: CSV, total XP first row second column for 'overall'
                var url = $"https://secure.runescape.com/m=hiscore/index_lite.ws?player={Uri.EscapeDataString(rsn)}";
                var csv = await _http.GetStringAsync(url, ct);
                var firstLine = csv.Split('\n')[0];
                var parts = firstLine.Split(',');
                if (parts.Length >= 3 && long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var xp))
                    return xp;
                return null;
            }
            catch { return null; }
        }
    }
}
