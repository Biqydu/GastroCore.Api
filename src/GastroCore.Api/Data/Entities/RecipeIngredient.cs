using GastroCore.Api.Data.Entities.Abstractions;

namespace GastroCore.Api.Data.Entities;

public sealed class RecipeIngredient : IModifiedTimestamps
{
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    public required Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    public required decimal AmountRequired { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}