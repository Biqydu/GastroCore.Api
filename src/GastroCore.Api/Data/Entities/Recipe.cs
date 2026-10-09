using GastroCore.Api.Data.Entities.Abstractions;

namespace GastroCore.Api.Data.Entities;

public sealed class Recipe : BaseEntity, IModifiedTimestamps
{
    public required string Name { get; set; }
    public required Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;
    public required decimal IngredientsTotalPrice { get; set; }

    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new HashSet<RecipeIngredient>();

    public required Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}