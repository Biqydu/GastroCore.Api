using GastroCore.Api.Data.Entities;
using GastroCore.Api.Features.Ingredients;
using GastroCore.Tests.Infrastructure;
using Shouldly;

namespace GastroCore.Tests.Features.Ingredients;

public sealed class GetWarehouseAlertsHandlerTests(TestWebAppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Handle_NoIngredients_ReturnsEmptyPage()
    {
        var result = await Send(new GetWarehouseAlerts.Query(1, 10));

        result.Data.ShouldBeEmpty();
        result.TotalRecords.ShouldBe(0);
        result.TotalPages.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyIngredientsAtOrBelowThreshold()
    {
        var below = Seed.CreateIngredient(name: "Below", stockQuantity: 5m, minStockThreshold: 10m);
        var equal = Seed.CreateIngredient(name: "Equal", stockQuantity: 10m, minStockThreshold: 10m);
        var above = Seed.CreateIngredient(name: "Above", stockQuantity: 11m, minStockThreshold: 10m);

        await SeedAsync(below, equal, above);

        var result = await Send(new GetWarehouseAlerts.Query(1, 10));

        result.TotalRecords.ShouldBe(2);
        result.Data.Select(i => i.Name).ShouldBe(["Below", "Equal"], ignoreOrder: true);
        result.Data.ShouldNotContain(i => i.Name == "Above");
    }

    [Fact]
    public async Task Handle_MapsAllFieldsToDto()
    {
        var ingredient = Seed.CreateIngredient(
            unitCost: 2.5m, name: "Flour", stockQuantity: 3m, minStockThreshold: 10m);

        await SeedAsync(ingredient);

        var result = await Send(new GetWarehouseAlerts.Query(1, 10));

        var dto = result.Data.Single();
        dto.Id.ShouldBe(ingredient.Id);
        dto.Name.ShouldBe("Flour");
        dto.StockQuantity.ShouldBe(3m);
        dto.UnitCost.ShouldBe(2.5m);
        dto.MinStockThreshold.ShouldBe(10m);
    }

    [Fact]
    public async Task Handle_OrdersByShortageThenByName()
    {
        var low = Seed.CreateIngredient(name: "Low", stockQuantity: 8m, minStockThreshold: 10m);
        var critical = Seed.CreateIngredient(name: "Critical", stockQuantity: 0m, minStockThreshold: 10m);
        var beta = Seed.CreateIngredient(name: "Beta", stockQuantity: 10m, minStockThreshold: 10m);
        var alpha = Seed.CreateIngredient(name: "Alpha", stockQuantity: 20m, minStockThreshold: 20m);

        await SeedAsync(low, critical, beta, alpha);

        var result = await Send(new GetWarehouseAlerts.Query(1, 10));

        result.Data.Select(i => i.Name).ShouldBe(["Critical", "Low", "Alpha", "Beta"]);
    }

    [Fact]
    public async Task Handle_Paginates_ReturnsCorrectPageAndMetadata()
    {
        var ingredients = Enumerable.Range(0, 5)
            .Select(i => Seed.CreateIngredient(
                name: $"I{i}", stockQuantity: i, minStockThreshold: 10m))
            .ToArray();

        await SeedAsync(ingredients);

        var page2 = await Send(new GetWarehouseAlerts.Query(2, 2));

        page2.Data.Select(i => i.Name).ShouldBe(["I2", "I3"]);
        page2.TotalRecords.ShouldBe(5);
        page2.TotalPages.ShouldBe(3);
        page2.PageNumber.ShouldBe(2);
        page2.PageSize.ShouldBe(2);

        var lastPage = await Send(new GetWarehouseAlerts.Query(3, 2));

        lastPage.Data.Select(i => i.Name).ShouldBe(["I4"]);
    }

    [Fact]
    public async Task Handle_PageBeyondLastPage_ReturnsEmptyDataButKeepsTotals()
    {
        await SeedAsync(Seed.CreateIngredient(stockQuantity: 1m, minStockThreshold: 10m));

        var result = await Send(new GetWarehouseAlerts.Query(5, 10));

        result.Data.ShouldBeEmpty();
        result.TotalRecords.ShouldBe(1);
        result.TotalPages.ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Handle_PageNumberBelowOne_IsClampedToFirstPage(int pageNumber)
    {
        await SeedAsync(Seed.CreateIngredient(stockQuantity: 1m, minStockThreshold: 10m));

        var result = await Send(new GetWarehouseAlerts.Query(pageNumber, 10));

        result.PageNumber.ShouldBe(1);
        result.Data.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(51, 50)]
    [InlineData(1000, 50)]
    public async Task Handle_PageSizeOutOfRange_IsClamped(int requested, int expected)
    {
        await SeedAsync(Seed.CreateIngredient(stockQuantity: 1m, minStockThreshold: 10m));

        var result = await Send(new GetWarehouseAlerts.Query(1, requested));

        result.PageSize.ShouldBe(expected);
    }

    private Task SeedAsync(params Ingredient[] ingredients) =>
        ExecuteDb(async db =>
        {
            db.Ingredients.AddRange(ingredients);
            await db.SaveChangesAsync();
        });
}