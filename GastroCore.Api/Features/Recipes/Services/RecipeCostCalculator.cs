using ErrorOr;
using GastroCore.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Recipes.Services;

public sealed class RecipeCostCalculator(AppDbContext db) : IRecipeCostCalculator
{
    public async Task<ErrorOr<decimal>> CalculateBasePriceAsync(
        IReadOnlyList<(Guid IngredientId, decimal AmountRequired)> items,
        CancellationToken ct = default)
    {
        if (items.Count == 0)
            return 0m;

        var ingredientIds = items
            .Select(i => i.IngredientId)
            .ToHashSet();

        var ingredientsFromDb = await db.Ingredients
            .Where(i => ingredientIds.Contains(i.Id))
            .Select(i => new { i.Id, i.UnitCost })
            .ToArrayAsync(ct);

        if (ingredientsFromDb.Length != ingredientIds.Count)
            return Error.Validation(
                "Recipe.InvalidIngredients",
                "One or more provided ingredients do not exist.");

        var ingredientCosts = ingredientsFromDb.ToDictionary(i => i.Id, i => i.UnitCost);

        return items.Sum(item => item.AmountRequired * ingredientCosts[item.IngredientId]);
    }
}