using ErrorOr;

namespace GastroCore.Api.Features.Recipes.Common;

public static class RecipeCostCalculator
{
    public static ErrorOr<decimal> CalculateIngredientsTotalPrice(
        IEnumerable<(Guid IngredientId, decimal AmountRequired)> items,
        IReadOnlyDictionary<Guid, decimal> unitCosts)
    {
        var total = 0m;

        foreach (var (ingredientId, amountRequired) in items)
        {
            if (!unitCosts.TryGetValue(ingredientId, out var unitCost))
                return Error.Validation(
                    "Recipe.InvalidIngredients",
                    "One or more provided ingredients do not exist.");

            total += amountRequired * unitCost;
        }

        return total;
    }
}