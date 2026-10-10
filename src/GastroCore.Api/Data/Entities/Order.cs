using GastroCore.Api.Data.Entities.Abstractions;

namespace GastroCore.Api.Data.Entities;

public sealed class Order : BaseEntity, IModifiedTimestamps
{
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public required OrderSource Source { get; set; }

    public required string ExternalOrderId { get; set; }
    public required decimal TotalPrice { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = new HashSet<OrderItem>();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}