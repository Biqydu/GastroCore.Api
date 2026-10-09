using ErrorOr;
using GastroCore.Api.Features.Recipes;
using GastroCore.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace GastroCore.Tests.Features.Recipes;

public sealed class CreateRecipeHandlerTests(TestWebAppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Handle_ValidCommand_CreatesRecipeWithCalculatedIngredientsTotalPrice()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(3m, "Flour");
        var sugar = Seed.CreateIngredient(5m, "Sugar");

        await ExecuteDb(async db =>
        {
            db.Brands.Add(brand);
            db.Ingredients.AddRange(flour, sugar);
            await db.SaveChangesAsync();
        });

        var result = await Send(new CreateRecipeCommand(
            "Cake",
            brand.Id,
            [
                new CreateRecipeIngredientDto(flour.Id, 2m), // 2 * 3 = 6
                new CreateRecipeIngredientDto(sugar.Id, 0.5m) // 0.5 * 5 = 2.5
            ]));

        result.IsError.ShouldBeFalse();
        result.Value.Name.ShouldBe("Cake");
        result.Value.BrandId.ShouldBe(brand.Id);
        result.Value.IngredientsTotalPrice.ShouldBe(8.5m);
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsRecipeWithIngredientsAndCreator()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(3m);

        await ExecuteDb(async db =>
        {
            db.Brands.Add(brand);
            db.Ingredients.Add(flour);
            await db.SaveChangesAsync();
        });

        var result = await Send(new CreateRecipeCommand(
            "Bread",
            brand.Id,
            [new CreateRecipeIngredientDto(flour.Id, 4m)]));

        result.IsError.ShouldBeFalse();

        var saved = await QueryDb(db => db.Recipes
            .Include(r => r.RecipeIngredients)
            .SingleAsync(r => r.Id == result.Value.RecipeId));

        saved.Name.ShouldBe("Bread");
        saved.BrandId.ShouldBe(brand.Id);
        saved.IngredientsTotalPrice.ShouldBe(12m);
        saved.CreatedBy.ShouldBe(Factory.CurrentUser.Id);
        saved.RecipeIngredients.Count.ShouldBe(1);
        saved.RecipeIngredients.Single().IngredientId.ShouldBe(flour.Id);
        saved.RecipeIngredients.Single().AmountRequired.ShouldBe(4m);
    }

    [Fact]
    public async Task Handle_BrandNotFound_ReturnsNotFoundAndSavesNothing()
    {
        var flour = Seed.CreateIngredient();

        await ExecuteDb(async db =>
        {
            db.Ingredients.Add(flour);
            await db.SaveChangesAsync();
        });

        var result = await Send(new CreateRecipeCommand(
            "Ghost recipe",
            Guid.NewGuid(),
            [new CreateRecipeIngredientDto(flour.Id, 1m)]));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("Brand.NotFound");

        (await QueryDb(db => db.Recipes.CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_UnknownIngredient_ReturnsValidationErrorAndSavesNothing()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient();

        await ExecuteDb(async db =>
        {
            db.Brands.Add(brand);
            db.Ingredients.Add(flour);
            await db.SaveChangesAsync();
        });

        var result = await Send(new CreateRecipeCommand(
            "Broken recipe",
            brand.Id,
            [
                new CreateRecipeIngredientDto(flour.Id, 1m),
                new CreateRecipeIngredientDto(Guid.NewGuid(), 1m) // nie istnieje
            ]));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe("Recipe.InvalidIngredients");

        (await QueryDb(db => db.Recipes.CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_IngredientWithZeroUnitCost_ContributesNothingToIngredientsTotalPrice()
    {
        var brand = Seed.CreateBrand();
        var water = Seed.CreateIngredient(0m, "Water");
        var flour = Seed.CreateIngredient(3m, "Flour");

        await ExecuteDb(async db =>
        {
            db.Brands.Add(brand);
            db.Ingredients.AddRange(water, flour);
            await db.SaveChangesAsync();
        });

        var result = await Send(new CreateRecipeCommand(
            "Dough",
            brand.Id,
            [
                new CreateRecipeIngredientDto(water.Id, 10m),
                new CreateRecipeIngredientDto(flour.Id, 2m)
            ]));

        result.IsError.ShouldBeFalse();
        result.Value.IngredientsTotalPrice.ShouldBe(6m);
    }
}