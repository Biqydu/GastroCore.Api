using System.Collections.Immutable;
using ErrorOr;
using GastroCore.Api.Features.Recipes.Common;
using Shouldly;

namespace GastroCore.Tests.Features.Recipes.Common;

public sealed class RecipeCostCalculatorTests
{
    private static readonly Guid FlourId = Guid.NewGuid();
    private static readonly Guid SugarId = Guid.NewGuid();
    private static readonly Guid EggId = Guid.NewGuid();

    public static TheoryData<decimal, decimal, decimal> VariousInputs => new()
    {
        { 1m, 1m, 1m },
        { 2m, 3m, 6m },
        { 10m, 0.25m, 2.5m },
        { 0.1m, 0.2m, 0.02m }
    };

    [Fact]
    public void CalculateIngredientsTotalPrice_EmptyItems_ReturnsZero()
    {
        var items = Array.Empty<(Guid, decimal)>();

        var result = RecipeCostCalculator.CalculateIngredientsTotalPrice(
            items, ImmutableDictionary<Guid, decimal>.Empty);

        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe(0m);
    }

    [Fact]
    public void CalculateIngredientsTotalPrice_SingleItem_ReturnsAmountTimesUnitCost()
    {
        var items = new[] { (FlourId, 2.5m) };
        var unitCosts = new Dictionary<Guid, decimal> { [FlourId] = 4m };

        var result = RecipeCostCalculator.CalculateIngredientsTotalPrice(items, unitCosts);

        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe(10m);
    }

    [Fact]
    public void CalculateIngredientsTotalPrice_MultipleItems_ReturnsSumOfCosts()
    {
        var items = new[]
        {
            (FlourId, 2m), // 2 * 3.00 = 6.00
            (SugarId, 0.5m), // 0.5 * 5.00 = 2.50
            (EggId, 4m) // 4 * 0.75 = 3.00
        };
        var unitCosts = new Dictionary<Guid, decimal>
        {
            [FlourId] = 3m,
            [SugarId] = 5m,
            [EggId] = 0.75m
        };

        var result = RecipeCostCalculator.CalculateIngredientsTotalPrice(items, unitCosts);

        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe(11.5m);
    }

    [Fact]
    public void CalculateIngredientsTotalPrice_UnknownIngredient_ReturnsValidationError()
    {
        var items = new[] { (FlourId, 1m), (SugarId, 1m) };
        var unitCosts = new Dictionary<Guid, decimal> { [FlourId] = 3m };

        var result = RecipeCostCalculator.CalculateIngredientsTotalPrice(items, unitCosts);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Code.ShouldBe("Recipe.InvalidIngredients");
    }

    [Fact]
    public void CalculateIngredientsTotalPrice_EmptyUnitCosts_ReturnsValidationError()
    {
        var items = new[] { (FlourId, 1m) };

        var result = RecipeCostCalculator.CalculateIngredientsTotalPrice(
            items, ImmutableDictionary<Guid, decimal>.Empty);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Recipe.InvalidIngredients");
    }

    [Fact]
    public void CalculateIngredientsTotalPrice_ZeroAmount_ContributesNothing()
    {
        var items = new[] { (FlourId, 0m), (SugarId, 2m) };
        var unitCosts = new Dictionary<Guid, decimal>
        {
            [FlourId] = 100m,
            [SugarId] = 5m
        };

        var result = RecipeCostCalculator.CalculateIngredientsTotalPrice(items, unitCosts);

        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe(10m);
    }

    [Fact]
    public void CalculateIngredientsTotalPrice_ZeroUnitCost_ContributesNothing()
    {
        var items = new[] { (FlourId, 5m), (SugarId, 2m) };
        var unitCosts = new Dictionary<Guid, decimal>
        {
            [FlourId] = 0m,
            [SugarId] = 5m
        };

        var result = RecipeCostCalculator.CalculateIngredientsTotalPrice(items, unitCosts);

        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe(10m);
    }

    [Fact]
    public void CalculateIngredientsTotalPrice_DuplicateIngredientIds_SumsEachOccurrence()
    {
        var items = new[] { (FlourId, 1m), (FlourId, 2m) };
        var unitCosts = new Dictionary<Guid, decimal> { [FlourId] = 3m };

        var result = RecipeCostCalculator.CalculateIngredientsTotalPrice(items, unitCosts);

        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe(9m);
    }

    [Fact]
    public void CalculateIngredientsTotalPrice_ExtraUnitCostsNotInItems_AreIgnored()
    {
        var items = new[] { (FlourId, 2m) };
        var unitCosts = new Dictionary<Guid, decimal>
        {
            [FlourId] = 3m,
            [SugarId] = 999m
        };

        var result = RecipeCostCalculator.CalculateIngredientsTotalPrice(items, unitCosts);

        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe(6m);
    }

    [Fact]
    public void CalculateIngredientsTotalPrice_DecimalPrecision_IsPreserved()
    {
        var items = new[] { (FlourId, 0.333m) };
        var unitCosts = new Dictionary<Guid, decimal> { [FlourId] = 1.5m };

        var result = RecipeCostCalculator.CalculateIngredientsTotalPrice(items, unitCosts);

        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe(0.4995m);
    }

    [Theory]
    [MemberData(nameof(VariousInputs))]
    public void CalculateIngredientsTotalPrice_VariousInputs_ReturnsExpectedTotal(
        decimal amount, decimal unitCost, decimal expected)
    {
        var items = new[] { (FlourId, amount) };
        var unitCosts = new Dictionary<Guid, decimal> { [FlourId] = unitCost };

        var result = RecipeCostCalculator.CalculateIngredientsTotalPrice(items, unitCosts);

        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe(expected);
    }
}