using System.Collections.Generic;

namespace RS3ClanHelper.Models
{
    public record RoleDelta(ulong UserId, string DesiredRoleName, ulong DesiredRoleId, List<ulong> RemoveRoleIds);
}
