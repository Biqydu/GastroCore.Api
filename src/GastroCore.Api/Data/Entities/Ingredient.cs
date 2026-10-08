using GastroCore.Api.Data.Entities.Abstractions;

namespace GastroCore.Api.Data.Entities;

public sealed class Ingredient : BaseEntity, IModifiedTimestamps
{
    public required string Name { get; set; }
    public required decimal StockQuantity { get; set; }
    public required decimal UnitCost { get; set; }
    public required decimal MinStockThreshold { get; set; }
    public required Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}