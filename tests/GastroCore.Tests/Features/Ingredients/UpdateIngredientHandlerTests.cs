using ErrorOr;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Features.Ingredients;
using GastroCore.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace GastroCore.Tests.Features.Ingredients;

public sealed class HandlerTests(TestWebAppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Handle_UnitCostChanged_RecalculatesIngredientsTotalPriceOfRecipeUsingNewCost()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(3m, "Flour");
        var recipe = Seed.CreateRecipe(brand.Id, 6m, items: (flour, 2m));

        await SeedAsync(brand, [flour], [recipe]);

        var result = await Send(new UpdateIngredient.Command(
            flour.Id, "Flour", 100,
            5m, 10));

        result.IsError.ShouldBeFalse();

        var updated = await QueryDb(db => db.Recipes.SingleAsync(r => r.Id == recipe.Id));
        updated.IngredientsTotalPrice.ShouldBe(10m); // 2 * 5
    }

    [Fact]
    public async Task Handle_UnitCostChanged_UsesCurrentCostsOfOtherIngredientsInRecipe()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(3m, "Flour");
        var sugar = Seed.CreateIngredient(5m, "Sugar");
        var recipe = Seed.CreateRecipe(
            brand.Id, 8.5m, items: [(flour, 2m), (sugar, 0.5m)]);

        await SeedAsync(brand, [flour, sugar], [recipe]);

        await Send(new UpdateIngredient.Command(
            flour.Id, "Flour", 100,
            4m, 10));

        var updated = await QueryDb(db => db.Recipes.SingleAsync(r => r.Id == recipe.Id));
        updated.IngredientsTotalPrice.ShouldBe(10.5m); // 2 * 4 + 0.5 * 5
    }

    [Fact]
    public async Task Handle_UnitCostChanged_RecalculatesAllRecipesContainingIngredient()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(3m, "Flour");
        var bread = Seed.CreateRecipe(brand.Id, 6m, "Bread", items: (flour, 2m));
        var cake = Seed.CreateRecipe(brand.Id, 15m, "Cake", items: (flour, 5m));

        await SeedAsync(brand, [flour], [bread, cake]);

        await Send(new UpdateIngredient.Command(
            flour.Id, "Flour", 100,
            10m, 10));

        var prices = await QueryDb(db => db.Recipes
            .ToDictionaryAsync(r => r.Id, r => r.IngredientsTotalPrice));

        prices[bread.Id].ShouldBe(20m);
        prices[cake.Id].ShouldBe(50m);
    }

    [Fact]
    public async Task Handle_UnitCostChanged_DoesNotTouchRecipesWithoutIngredient()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(3m, "Flour");
        var sugar = Seed.CreateIngredient(5m, "Sugar");
        var unrelated = Seed.CreateRecipe(brand.Id, 999m, "Candy", items: (sugar, 1m));

        await SeedAsync(brand, [flour, sugar], [unrelated]);

        await Send(new UpdateIngredient.Command(
            flour.Id, "Flour", 100,
            10m, 10));

        var untouched = await QueryDb(db => db.Recipes.SingleAsync(r => r.Id == unrelated.Id));
        untouched.IngredientsTotalPrice.ShouldBe(999m);
    }

    [Fact]
    public async Task Handle_UnitCostUnchanged_DoesNotRecalculateRecipes()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(3m, "Flour");
        var recipe = Seed.CreateRecipe(brand.Id, 999m, items: (flour, 2m));

        await SeedAsync(brand, [flour], [recipe]);

        var result = await Send(new UpdateIngredient.Command(
            flour.Id, "Flour renamed", 50,
            3m, 10));

        result.IsError.ShouldBeFalse();

        var untouched = await QueryDb(db => db.Recipes.SingleAsync(r => r.Id == recipe.Id));
        untouched.IngredientsTotalPrice.ShouldBe(999m);
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsIngredientChanges()
    {
        var ingredient = Seed.CreateIngredient(3m, "Flour");

        await ExecuteDb(async db =>
        {
            db.Ingredients.Add(ingredient);
            await db.SaveChangesAsync();
        });

        var result = await Send(new UpdateIngredient.Command(
            ingredient.Id, "Rye flour", 42,
            4.5m, 7));

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
        var result = await Send(new UpdateIngredient.Command(
            Guid.NewGuid(), "X", 1,
            1m, 1));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("Ingredient.NotFound");
    }

    private Task SeedAsync(Brand brand, Ingredient[] ingredients, Recipe[] recipes)
    {
        return ExecuteDb(async db =>
        {
            db.Brands.Add(brand);
            db.Ingredients.AddRange(ingredients);
            db.Recipes.AddRange(recipes);
            await db.SaveChangesAsync();
        });
    }
}