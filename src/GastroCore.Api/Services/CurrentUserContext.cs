using System.Security.Claims;
using GastroCore.Api.Data.Entities;

namespace GastroCore.Api.Services;

public sealed class CurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    private HttpContext? HttpContext => httpContextAccessor.HttpContext;

    public Guid Id
    {
        get
        {
            var userIdClaim = HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return !Guid.TryParse(userIdClaim, out var userId)
                ? throw new UnauthorizedAccessException(
                    "User is not logged in, or a valid ID is missing.")
                : userId;
        }
    }

    public bool IsAuthenticated => HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(UserRole role)
    {
        return HttpContext?.User.IsInRole(role.ToString()) ?? false;
    }

    public UserRole GetRole()
    {
        var role = HttpContext?.User.FindFirstValue(ClaimTypes.Role);

        return !Enum.TryParse<UserRole>(role, out var result)
            ? throw new InvalidOperationException("User role claim is missing or invalid.")
            : result;
    }

    public bool IsChefOnly => IsInRole(UserRole.Chef) && !IsInRole(UserRole.Manager);
}