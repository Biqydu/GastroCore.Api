using GastroCore.Api.Data.Entities;

namespace GastroCore.Api.Services;

public interface ICurrentUserContext
{
    Guid Id { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(UserRole role);
    
    bool IsChefOnly { get; }
}