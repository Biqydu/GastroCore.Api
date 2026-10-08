using Microsoft.AspNetCore.Identity;

namespace GastroCore.Api.Data.Entities;

public sealed class AppUser : IdentityUser<Guid>
{
    public AppUser()
    {
        Id = Guid.CreateVersion7();
    }
}