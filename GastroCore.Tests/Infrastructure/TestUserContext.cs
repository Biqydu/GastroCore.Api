using GastroCore.Api.Data.Entities;
using GastroCore.Api.Services;

namespace GastroCore.Tests.Infrastructure;

public sealed class TestUserContext : ICurrentUserContext
{
    private readonly HashSet<UserRole> _roles = [UserRole.Manager];

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public bool IsAuthenticated { get; set; } = true;

    public bool IsInRole(UserRole role)
    {
        return _roles.Contains(role);
    }

    public bool IsChefOnly => IsInRole(UserRole.Chef) && !IsInRole(UserRole.Manager);

    public TestUserContext WithRoles(params UserRole[] roles)
    {
        _roles.Clear();
        foreach (var role in roles)
            _roles.Add(role);

        return this;
    }

    public TestUserContext Anonymous()
    {
        IsAuthenticated = false;
        _roles.Clear();
        return this;
    }

    public void Reset()
    {
        Id = Guid.CreateVersion7();
        IsAuthenticated = true;
        _roles.Clear();
        _roles.Add(UserRole.Manager);
    }
}