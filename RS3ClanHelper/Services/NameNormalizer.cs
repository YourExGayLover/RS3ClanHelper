using System.Text.RegularExpressions;

namespace RS3ClanHelper.Services
{
    public class NameNormalizer : INameNormalizer
    {
        public string Normalize(string s) =>
            Regex.Replace(s ?? string.Empty, @"\s+", "").Trim().ToLowerInvariant();
    }
}
