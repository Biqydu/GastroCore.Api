using GastroCore.Api.Data.Entities.Abstractions;

namespace GastroCore.Api.Data.Entities;

public sealed class Brand : BaseEntity, IModifiedTimestamps
{
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public required Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}