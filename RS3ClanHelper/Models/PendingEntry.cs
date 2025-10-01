using System.Collections.Generic;

namespace RS3ClanHelper.Models
{
    /// <summary>Pending set of role changes awaiting confirmation.</summary>
    public class PendingEntry
    {
        public ulong GuildId { get; set; }
        public ulong RequestorId { get; set; }
        public List<RoleDelta> Deltas { get; set; } = new();
    }
}
