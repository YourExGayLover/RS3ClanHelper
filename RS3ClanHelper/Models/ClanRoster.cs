using System.Collections.Generic;

namespace RS3ClanHelper.Models
{
    public record ClanRoster(string ClanName, List<ClanMember> Members);
}
