using GastroCore.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Common;

public static class IngredientQueries
{
    public static async Task<Dictionary<Guid, decimal>> GetUnitCostsAsync(
        this AppDbContext db,
        IEnumerable<Guid> ingredientIds,
        CancellationToken ct = default)
    {
        var ids = ingredientIds.Distinct().ToList();

        var rows = await db.Ingredients
            .Where(i => ids.Contains(i.Id))
            .Select(i => new { i.Id, i.UnitCost })
            .ToArrayAsync(ct);

        return rows.ToDictionary(i => i.Id, i => i.UnitCost);
    }
}