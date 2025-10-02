using System;
using System.Collections.Generic;

namespace RS3ClanHelper.Models
{
    public class ActivitySnapshot
    {
        public DateTimeOffset TakenAt { get; set; }
        // rsn (normalized) -> total XP
        public Dictionary<string, long> TotalXpByRsn { get; set; } = new();
    }
}
