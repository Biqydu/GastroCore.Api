using GastroCore.Api.Data.Entities.Abstractions;

namespace GastroCore.Api.Data.Entities;

public sealed class OrderItem : BaseEntity
{
    public required Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    
    public required Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    
    public required int Quantity { get; set; }
    public required decimal UnitPrice { get; set; }
}