using ErrorOr;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Features.Orders;
using GastroCore.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace GastroCore.Tests.Features.Orders;

public sealed class PlaceOrderHandlerTests(TestWebAppFactory factory) : IntegrationTestBase(factory)
{
    private const OrderSource Source = OrderSource.Internal;

    [Fact]
    public async Task Handle_ValidCommand_ReturnsOrderWithCalculatedTotalPrice()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(name: "Flour", stockQuantity: 100m);
        var bread = Seed.CreateRecipe(brand.Id, 6m, "Bread", items: (flour, 2m));
        var cake = Seed.CreateRecipe(brand.Id, 10m, "Cake", items: (flour, 3m));

        await SeedAsync(brand, [flour], [bread, cake]);

        var result = await Send(new PlaceOrder.Command(
            Source,
            "EXT-1",
            [
                new PlaceOrder.OrderItemRequestDto(bread.Id, 2), // 2 * 6  = 12
                new PlaceOrder.OrderItemRequestDto(cake.Id, 1) // 1 * 10 = 10
            ]));

        result.IsError.ShouldBeFalse();
        result.Value.OrderId.ShouldNotBe(Guid.Empty);
        result.Value.Status.ShouldBe(OrderStatus.Pending);
        result.Value.Source.ShouldBe(Source);
        result.Value.ExternalOrderId.ShouldBe("EXT-1");
        result.Value.TotalPrice.ShouldBe(22m);
        result.Value.OrderItems.Count.ShouldBe(2);
        result.Value.OrderItems.ShouldContain(i =>
            i.RecipeId == bread.Id && i.Quantity == 2 && i.UnitPrice == 6m);
        result.Value.OrderItems.ShouldContain(i =>
            i.RecipeId == cake.Id && i.Quantity == 1 && i.UnitPrice == 10m);
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsOrderWithItems()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(stockQuantity: 100m);
        var bread = Seed.CreateRecipe(brand.Id, 6m, "Bread", items: (flour, 2m));

        await SeedAsync(brand, [flour], [bread]);

        var result = await Send(new PlaceOrder.Command(
            Source, "EXT-2", [new PlaceOrder.OrderItemRequestDto(bread.Id, 3)]));

        result.IsError.ShouldBeFalse();

        var saved = await QueryDb(db => db.Orders
            .Include(o => o.OrderItems)
            .SingleAsync(o => o.Id == result.Value.OrderId));

        saved.ExternalOrderId.ShouldBe("EXT-2");
        saved.Source.ShouldBe(Source);
        saved.Status.ShouldBe(OrderStatus.Pending);
        saved.TotalPrice.ShouldBe(18m);
        saved.OrderItems.Count.ShouldBe(1);

        var item = saved.OrderItems.Single();
        item.RecipeId.ShouldBe(bread.Id);
        item.Quantity.ShouldBe(3);
        item.UnitPrice.ShouldBe(6m);
    }

    [Fact]
    public async Task Handle_ValidCommand_DecrementsIngredientStock()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(name: "Flour", stockQuantity: 100m);
        var bread = Seed.CreateRecipe(brand.Id, 6m, "Bread", items: (flour, 2m));

        await SeedAsync(brand, [flour], [bread]);

        var result = await Send(new PlaceOrder.Command(
            Source, "EXT-3", [new PlaceOrder.OrderItemRequestDto(bread.Id, 5)]));

        result.IsError.ShouldBeFalse();

        var stock = await QueryDb(db => db.Ingredients
            .Where(i => i.Id == flour.Id)
            .Select(i => i.StockQuantity)
            .SingleAsync());

        stock.ShouldBe(90m); // 100 - 2 * 5
    }

    [Fact]
    public async Task Handle_RecipesShareIngredient_DecrementsStockByCombinedAmount()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(name: "Flour", stockQuantity: 100m);
        var sugar = Seed.CreateIngredient(name: "Sugar", stockQuantity: 50m);
        var bread = Seed.CreateRecipe(brand.Id, 6m, "Bread", items: (flour, 2m));
        var cake = Seed.CreateRecipe(brand.Id, 10m, "Cake", items: [(flour, 3m), (sugar, 1m)]);

        await SeedAsync(brand, [flour, sugar], [bread, cake]);

        var result = await Send(new PlaceOrder.Command(
            Source,
            "EXT-4",
            [
                new PlaceOrder.OrderItemRequestDto(bread.Id, 2), // flour: 4
                new PlaceOrder.OrderItemRequestDto(cake.Id, 3) // flour: 9, sugar: 3
            ]));

        result.IsError.ShouldBeFalse();

        var stocks = await QueryDb(db => db.Ingredients
            .ToDictionaryAsync(i => i.Id, i => i.StockQuantity));

        stocks[flour.Id].ShouldBe(87m); // 100 - 4 - 9
        stocks[sugar.Id].ShouldBe(47m); // 50 - 3
    }

    [Fact]
    public async Task Handle_SameRecipeInMultipleItems_AggregatesStockUsage()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(stockQuantity: 100m);
        var bread = Seed.CreateRecipe(brand.Id, 6m, "Bread", items: (flour, 2m));

        await SeedAsync(brand, [flour], [bread]);

        var result = await Send(new PlaceOrder.Command(
            Source,
            "EXT-5",
            [
                new PlaceOrder.OrderItemRequestDto(bread.Id, 1),
                new PlaceOrder.OrderItemRequestDto(bread.Id, 2)
            ]));

        result.IsError.ShouldBeFalse();
        result.Value.TotalPrice.ShouldBe(18m);

        var stock = await QueryDb(db => db.Ingredients
            .Where(i => i.Id == flour.Id)
            .Select(i => i.StockQuantity)
            .SingleAsync());

        stock.ShouldBe(94m); // 100 - 2 * 3
    }

    [Fact]
    public async Task Handle_StockExactlyEqualsRequired_Succeeds()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(stockQuantity: 10m);
        var bread = Seed.CreateRecipe(brand.Id, 6m, "Bread", items: (flour, 2m));

        await SeedAsync(brand, [flour], [bread]);

        var result = await Send(new PlaceOrder.Command(
            Source, "EXT-6", [new PlaceOrder.OrderItemRequestDto(bread.Id, 5)]));

        result.IsError.ShouldBeFalse();

        var stock = await QueryDb(db => db.Ingredients
            .Where(i => i.Id == flour.Id)
            .Select(i => i.StockQuantity)
            .SingleAsync());

        stock.ShouldBe(0m);
    }

    [Fact]
    public async Task Handle_InsufficientStock_ReturnsConflictAndChangesNothing()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(name: "Flour", stockQuantity: 9m);
        var bread = Seed.CreateRecipe(brand.Id, 6m, "Bread", items: (flour, 2m));

        await SeedAsync(brand, [flour], [bread]);

        var result = await Send(new PlaceOrder.Command(
            Source, "EXT-7", [new PlaceOrder.OrderItemRequestDto(bread.Id, 5)])); // potrzeba 10

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe("Order.InsufficientStock");
        result.FirstError.Description.ShouldContain("Flour");

        (await QueryDb(db => db.Orders.CountAsync())).ShouldBe(0);

        var stock = await QueryDb(db => db.Ingredients
            .Where(i => i.Id == flour.Id)
            .Select(i => i.StockQuantity)
            .SingleAsync());

        stock.ShouldBe(9m);
    }

    [Fact]
    public async Task Handle_StockSufficientPerRecipeButNotCombined_ReturnsConflict()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient(name: "Flour", stockQuantity: 10m);
        var bread = Seed.CreateRecipe(brand.Id, 6m, "Bread", items: (flour, 2m));
        var cake = Seed.CreateRecipe(brand.Id, 10m, "Cake", items: (flour, 3m));

        await SeedAsync(brand, [flour], [bread, cake]);

        // Osobno: bread x3 = 6, cake x2 = 6 (oba mieszczą się w 10). Razem: 12 > 10
        var result = await Send(new PlaceOrder.Command(
            Source,
            "EXT-8",
            [
                new PlaceOrder.OrderItemRequestDto(bread.Id, 3),
                new PlaceOrder.OrderItemRequestDto(cake.Id, 2)
            ]));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe("Order.InsufficientStock");

        (await QueryDb(db => db.Orders.CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_RecipeNotFound_ReturnsNotFoundAndSavesNothing()
    {
        var brand = Seed.CreateBrand();
        var flour = Seed.CreateIngredient();
        var bread = Seed.CreateRecipe(brand.Id, 6m, "Bread", items: (flour, 2m));

        await SeedAsync(brand, [flour], [bread]);

        var missingRecipeId = Guid.NewGuid();

        var result = await Send(new PlaceOrder.Command(
            Source,
            "EXT-9",
            [
                new PlaceOrder.OrderItemRequestDto(bread.Id, 1),
                new PlaceOrder.OrderItemRequestDto(missingRecipeId, 1)
            ]));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("Recipe.NotFound");
        result.FirstError.Description.ShouldContain(missingRecipeId.ToString());

        (await QueryDb(db => db.Orders.CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_RecipeFromInactiveBrand_ReturnsConflictAndChangesNothing()
    {
        var activeBrand = Seed.CreateBrand("Active");
        var inactiveBrand = Seed.CreateBrand("Inactive", false);
        var flour = Seed.CreateIngredient(stockQuantity: 100m);
        var activeRecipe = Seed.CreateRecipe(activeBrand.Id, 6m, "A", items: (flour, 1m));
        var inactiveRecipe = Seed.CreateRecipe(inactiveBrand.Id, 6m, "B", items: (flour, 1m));

        await ExecuteDb(async db =>
        {
            db.Brands.AddRange(activeBrand, inactiveBrand);
            db.Ingredients.Add(flour);
            db.Recipes.AddRange(activeRecipe, inactiveRecipe);
            await db.SaveChangesAsync();
        });

        var result = await Send(new PlaceOrder.Command(
            Source,
            "EXT-10",
            [
                new PlaceOrder.OrderItemRequestDto(activeRecipe.Id, 1),
                new PlaceOrder.OrderItemRequestDto(inactiveRecipe.Id, 1)
            ]));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe("Order.InactiveBrand");

        (await QueryDb(db => db.Orders.CountAsync())).ShouldBe(0);

        var stock = await QueryDb(db => db.Ingredients
            .Where(i => i.Id == flour.Id)
            .Select(i => i.StockQuantity)
            .SingleAsync());

        stock.ShouldBe(100m);
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