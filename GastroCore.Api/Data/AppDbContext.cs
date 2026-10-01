using GastroCore.Api.Data.Entities;
using GastroCore.Api.Data.Entities.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<
    AppUser,
    AppRole,
    Guid,
    IdentityUserClaim<Guid>,
    IdentityUserRole<Guid>,
    IdentityUserLogin<Guid>,
    IdentityRoleClaim<Guid>,
    IdentityUserToken<Guid>>(options)
{
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    
    protected override void OnModelCreating(ModelBuilder builder)
    { 
        base.OnModelCreating(builder);
        
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var entries = ChangeTracker.Entries();

        foreach (var entry in entries)
        {
            if (entry is { State: EntityState.Added, Entity: ICreatedTimestamps createdEntity })
            {
                createdEntity.CreatedAt = now;
            }
            
            if (entry is { State: EntityState.Modified, Entity: IModifiedTimestamps modifiedEntity })
            {
                modifiedEntity.UpdatedAt = now;
            }
        }
        
        return base.SaveChangesAsync(ct);
    }
}