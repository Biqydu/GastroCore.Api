using ErrorOr;

namespace GastroCore.Api.Features.Recipes.Services;

public interface IRecipeCostCalculator
{
    Task<ErrorOr<decimal>> CalculateBasePriceAsync(
        IReadOnlyList<(Guid IngredientId, decimal AmountRequired)> items,
        CancellationToken ct = default);
}