using ErrorOr;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Features.Ingredients;
using GastroCore.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace GastroCore.Tests.Features.Ingredients;

public sealed class UpdateIngredientHandlerTests(TestWebAppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Handle_UnitCostChanged_RecalculatesIngredientsTotalPriceOfRecipeUsingNewCost()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(unitCost: 3m, name: "Flour");
        var recipe = Seed.CreateRecipe(brand.Id, ingredientsTotalPrice: 6m, items: (flour, 2m));

        await SeedAsync(brand, [flour], [recipe]);

        var result = await Send(new UpdateIngredientCommand(
            Id: flour.Id, Name: "Flour", StockQuantity: 100,
            UnitCost: 5m, MinStockThreshold: 10));

        result.IsError.ShouldBeFalse();

        var updated = await QueryDb(db => db.Recipes.SingleAsync(r => r.Id == recipe.Id));
        updated.IngredientsTotalPrice.ShouldBe(10m); // 2 * 5
    }

    [Fact]
    public async Task Handle_UnitCostChanged_UsesCurrentCostsOfOtherIngredientsInRecipe()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(unitCost: 3m, name: "Flour");
        var sugar = Seed.CreateIngredient(unitCost: 5m, name: "Sugar");
        var recipe = Seed.CreateRecipe(
            brand.Id, ingredientsTotalPrice: 8.5m, items: [(flour, 2m), (sugar, 0.5m)]);

        await SeedAsync(brand, [flour, sugar], [recipe]);

        await Send(new UpdateIngredientCommand(
            Id: flour.Id, Name: "Flour", StockQuantity: 100,
            UnitCost: 4m, MinStockThreshold: 10));

        var updated = await QueryDb(db => db.Recipes.SingleAsync(r => r.Id == recipe.Id));
        updated.IngredientsTotalPrice.ShouldBe(10.5m); // 2 * 4 + 0.5 * 5
    }

    [Fact]
    public async Task Handle_UnitCostChanged_RecalculatesAllRecipesContainingIngredient()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(unitCost: 3m, name: "Flour");
        var bread = Seed.CreateRecipe(brand.Id, 6m, "Bread", items: (flour, 2m));
        var cake = Seed.CreateRecipe(brand.Id, 15m, "Cake", items: (flour, 5m));

        await SeedAsync(brand, [flour], [bread, cake]);

        await Send(new UpdateIngredientCommand(
            Id: flour.Id, Name: "Flour", StockQuantity: 100,
            UnitCost: 10m, MinStockThreshold: 10));

        var prices = await QueryDb(db => db.Recipes
            .ToDictionaryAsync(r => r.Id, r => r.IngredientsTotalPrice));

        prices[bread.Id].ShouldBe(20m);
        prices[cake.Id].ShouldBe(50m);
    }

    [Fact]
    public async Task Handle_UnitCostChanged_DoesNotTouchRecipesWithoutIngredient()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(unitCost: 3m, name: "Flour");
        var sugar = Seed.CreateIngredient(unitCost: 5m, name: "Sugar");
        var unrelated = Seed.CreateRecipe(brand.Id, 999m, "Candy", items: (sugar, 1m));

        await SeedAsync(brand, [flour, sugar], [unrelated]);

        await Send(new UpdateIngredientCommand(
            Id: flour.Id, Name: "Flour", StockQuantity: 100,
            UnitCost: 10m, MinStockThreshold: 10));

        var untouched = await QueryDb(db => db.Recipes.SingleAsync(r => r.Id == unrelated.Id));
        untouched.IngredientsTotalPrice.ShouldBe(999m);
    }

    [Fact]
    public async Task Handle_UnitCostUnchanged_DoesNotRecalculateRecipes()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(unitCost: 3m, name: "Flour");
        var recipe = Seed.CreateRecipe(brand.Id, ingredientsTotalPrice: 999m, items: (flour, 2m));

        await SeedAsync(brand, [flour], [recipe]);

        var result = await Send(new UpdateIngredientCommand(
            Id: flour.Id, Name: "Flour renamed", StockQuantity: 50,
            UnitCost: 3m, MinStockThreshold: 10));

        result.IsError.ShouldBeFalse();

        var untouched = await QueryDb(db => db.Recipes.SingleAsync(r => r.Id == recipe.Id));
        untouched.IngredientsTotalPrice.ShouldBe(999m);
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsIngredientChanges()
    {
        var ingredient = Seed.CreateIngredient(unitCost: 3m, name: "Flour");

        await ExecuteDb(async db =>
        {
            db.Ingredients.Add(ingredient);
            await db.SaveChangesAsync();
        });

        var result = await Send(new UpdateIngredientCommand(
            Id: ingredient.Id, Name: "Rye flour", StockQuantity: 42,
            UnitCost: 4.5m, MinStockThreshold: 7));

        result.IsError.ShouldBeFalse();
        result.Value.Id.ShouldBe(ingredient.Id);
        result.Value.Name.ShouldBe("Rye flour");

        var saved = await QueryDb(db => db.Ingredients.SingleAsync(i => i.Id == ingredient.Id));
        saved.Name.ShouldBe("Rye flour");
        saved.StockQuantity.ShouldBe(42);
        saved.UnitCost.ShouldBe(4.5m);
        saved.MinStockThreshold.ShouldBe(7);
    }

    [Fact]
    public async Task Handle_IngredientNotFound_ReturnsNotFound()
    {
        var result = await Send(new UpdateIngredientCommand(
            Id: Guid.NewGuid(), Name: "X", StockQuantity: 1,
            UnitCost: 1m, MinStockThreshold: 1));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("Ingredient.NotFound");
    }

    private Task SeedAsync(Brand brand, Ingredient[] ingredients, Recipe[] recipes) =>
        ExecuteDb(async db =>
        {
            db.Brands.Add(brand);
            db.Ingredients.AddRange(ingredients);
            db.Recipes.AddRange(recipes);
            await db.SaveChangesAsync();
        });
}