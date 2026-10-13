using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;

namespace GastroCore.Tests.Infrastructure;

public static class Seed
{
    public static readonly Guid DefaultCreatorId = Guid.CreateVersion7();

    public static Brand CreateBrand(
        string name = "Test brand",
        bool isActive = true,
        Guid? createdBy = null)
    {
        return new Brand
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsActive = isActive,
            CreatedBy = createdBy ?? DefaultCreatorId
        };
    }

    public static Ingredient CreateIngredient(
        decimal unitCost = 1m,
        string name = "Ingredient",
        decimal stockQuantity = 100m,
        decimal minStockThreshold = 10m,
        Guid? createdBy = null)
    {
        return new Ingredient
        {
            Id = Guid.NewGuid(),
            Name = name,
            StockQuantity = stockQuantity,
            UnitCost = unitCost,
            MinStockThreshold = minStockThreshold,
            CreatedBy = createdBy ?? DefaultCreatorId
        };
    }

    public static Recipe CreateRecipe(
        Guid brandId,
        decimal ingredientsTotalPrice,
        string name = "Recipe",
        Guid? createdBy = null,
        params (Ingredient Ingredient, decimal Amount)[] items)
    {
        return new Recipe
        {
            Name = name,
            BrandId = brandId,
            IngredientsTotalPrice = ingredientsTotalPrice,
            CreatedBy = createdBy ?? DefaultCreatorId,
            RecipeIngredients = items.Select(i => new RecipeIngredient
            {
                IngredientId = i.Ingredient.Id,
                AmountRequired = i.Amount
            }).ToList()
        };
    }

    public static Order CreateOrder(
        OrderSource source,
        string externalOrderId,
        decimal totalPrice,
        OrderStatus? status,
        params (Guid RecipeId, int Quantity, decimal UnitPrice)[] items)
    {
        var order = new Order
        {
            Source = source,
            ExternalOrderId = externalOrderId,
            TotalPrice = totalPrice,
            Status = status ?? OrderStatus.Pending
        };

        foreach (var item in items)
            order.OrderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                RecipeId = item.RecipeId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            });

        return order;
    }
}