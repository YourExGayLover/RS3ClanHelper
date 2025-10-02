using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RS3ClanHelper.Services
{
    // Minimal hiscore client using the classic index_lite endpoint.
    // Format: each line => rank,level,xp. First line is Overall.
    public class HiscoreClient : IHiscoreClient
    {
        private readonly HttpClient _http;
        public HiscoreClient(HttpClient http) => _http = http;

        public async Task<long?> GetTotalXpAsync(string rsn, CancellationToken ct = default)
        {
            var csv = await FetchIndexLiteAsync(rsn, ct);
            if (csv is null) return null;
            var firstLine = csv.Split('\n')[0];
            var parts = firstLine.Split(',');
            if (parts.Length >= 3 && long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var xp))
                return xp;
            return null;
        }

        public async Task<long?> GetOverallRankAsync(string rsn, CancellationToken ct = default)
        {
            var csv = await FetchIndexLiteAsync(rsn, ct);
            if (csv is null) return null;
            var firstLine = csv.Split('\n')[0];
            var parts = firstLine.Split(',');
            if (parts.Length >= 1 && long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank))
                return rank >= 0 ? rank : (long?)null;
            return null;
        }

        private async Task<string?> FetchIndexLiteAsync(string rsn, CancellationToken ct)
        {
            try
            {
                var url = $"https://secure.runescape.com/m=hiscore/index_lite.ws?player={Uri.EscapeDataString(rsn)}";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                using var res = await _http.SendAsync(req, ct);
                if (!res.IsSuccessStatusCode) return null;
                return await res.Content.ReadAsStringAsync(ct);
            }
            catch
            {
                return null;
            }
        }
    }
}
