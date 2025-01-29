using System.Security.Claims;
using System.Security.Principal;

namespace Ballware.Generic.Authorization;

public interface IPrincipalUtils
{
    public Guid GetUserId(ClaimsPrincipal principal);
    public Guid GetUserTenandId(ClaimsPrincipal principal);
    public Dictionary<string, object> GetUserClaims(ClaimsPrincipal principal);
    public IEnumerable<string> GetUserRights(ClaimsPrincipal principal);
}