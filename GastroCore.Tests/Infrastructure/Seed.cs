using GastroCore.Api.Data.Entities;

namespace GastroCore.Tests.Infrastructure;

public static class Seed
{
    public static readonly Guid DefaultCreatorId = Guid.CreateVersion7();

    public static Brand CreateBrand(string name = "Test brand", Guid? createdBy = null)
    {
        return new Brand
        {
            Name = name,
            CreatedBy = createdBy ?? DefaultCreatorId
        };
    }

    public static Ingredient CreateIngredient(
        decimal unitCost = 1m,
        string name = "Ingredient",
        Guid? createdBy = null)
    {
        return new Ingredient
        {
            Name = name,
            StockQuantity = 100,
            UnitCost = unitCost,
            MinStockThreshold = 10,
            CreatedBy = createdBy ?? DefaultCreatorId
        };
    }

    public static Recipe CreateRecipe(
        Guid brandId,
        decimal basePrice,
        string name = "Recipe",
        Guid? createdBy = null,
        params (Ingredient Ingredient, decimal Amount)[] items)
    {
        return new Recipe
        {
            Name = name,
            BrandId = brandId,
            BasePrice = basePrice,
            CreatedBy = createdBy ?? DefaultCreatorId,
            RecipeIngredients = items.Select(i => new RecipeIngredient
            {
                IngredientId = i.Ingredient.Id,
                AmountRequired = i.Amount
            }).ToList()
        };
    }
}