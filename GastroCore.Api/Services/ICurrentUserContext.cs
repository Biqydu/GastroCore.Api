namespace GastroCore.Api.Services;

public interface ICurrentUserContext
{
    Guid Id { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string roleName);
}