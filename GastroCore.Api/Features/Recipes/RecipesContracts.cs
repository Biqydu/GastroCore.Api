namespace GastroCore.Api.Features.Recipes;

public sealed record RecipeIngredientDto(Guid IngredientId, decimal AmountRequired);
public sealed record RecipeIngredientDetailsDto(Guid Id, string Name, decimal StockQuantity, decimal AmountRequired);