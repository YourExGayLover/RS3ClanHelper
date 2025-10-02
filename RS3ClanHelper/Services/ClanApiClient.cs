using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using RS3ClanHelper.Models;

namespace RS3ClanHelper.Services
{
    public class ClanApiClient : IClanApiClient
    {
        private readonly HttpClient _http;
        public ClanApiClient(HttpClient http) { _http = http; }

        public async Task<ClanRoster?> FetchClanAsync(string clanName, CancellationToken ct = default)
        {
            try
            {
                var url = $"https://services.runescape.com/m=clan-hiscores/members_lite.ws?clanName={Uri.EscapeDataString(clanName)}";
                var bytes = await _http.GetByteArrayAsync(url, ct);
                var text = Encoding.GetEncoding("ISO-8859-1").GetString(bytes);
                var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                var members = new List<ClanMember>();
                foreach (var line in lines)
                {
                    var cells = line.Split(',');
                    if (cells.Length < 4) continue;
                    var name = cells[0].Trim();
                    var rank = cells[1].Trim();
                    long.TryParse(cells[2], out var xp);
                    long.TryParse(cells[3], out var kills);
                    DateTimeOffset? join = null;
                    if (cells.Length >= 5)
                    {
                        var raw = cells[4]?.Trim();
                        if (!string.IsNullOrEmpty(raw))
                        {
                            // try common formats
                            string[] formats = new[] { "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-ddTHH:mm:ssK" };
                            if (DateTimeOffset.TryParseExact(raw, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var dt))
                                join = dt;
                            else if (DateTimeOffset.TryParse(raw, out var dt2))
                                join = dt2;
                        }
                    }
                    members.Add(new ClanMember(name, rank, xp, kills, join));
                }
                return new ClanRoster(clanName, members);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FetchClanAsync error: {ex.Message}");
                return null;
            }
        }
    }
}
