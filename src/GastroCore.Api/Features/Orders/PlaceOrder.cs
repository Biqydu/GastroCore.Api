using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using FluentValidation;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Orders;

public sealed record PlaceOrderRequest(
    OrderSource Source,
    string ExternalOrderId,
    IReadOnlyList<PlaceOrderOrderItemRequestDto> Items);

public sealed record PlaceOrderOrderItemRequestDto(Guid RecipeId, int Quantity);

public sealed record PlaceOrderOrderItemResponseDto(Guid RecipeId, int Quantity, decimal UnitPrice);

public sealed record PlaceOrderCommand(
    OrderSource Source,
    string ExternalOrderId,
    IReadOnlyList<PlaceOrderOrderItemRequestDto> Items) : IRequest<ErrorOr<PlaceOrderResponse>>;

public sealed record PlaceOrderResponse(
    Guid OrderId,
    OrderStatus Status,
    OrderSource Source,
    string ExternalOrderId,
    decimal TotalPrice,
    IReadOnlyList<PlaceOrderOrderItemResponseDto> OrderItems);

public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderCommand>
{
    private readonly string[] _validOrderSources = Enum.GetNames<OrderSource>();

    public PlaceOrderValidator()
    {
        RuleFor(x => x.Source)
            .IsInEnum()
            .WithMessage(
                $"{nameof(PlaceOrderCommand.Source)} is invalid. Valid values are {string.Join(", ", _validOrderSources)}");

        RuleFor(x => x.ExternalOrderId)
            .NotEmpty()
            .WithMessage("ExternalOrderId is required");

        RuleFor(x => x.Items)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one order item is required");

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(x => x.RecipeId)
                    .NotEmpty().WithMessage("RecipeId is required");

                item.RuleFor(x => x.Quantity)
                    .GreaterThan(0).WithMessage("Quantity must be greater than zero");
            });
    }
}

public sealed class PlaceOrderHandler(AppDbContext db)
    : IRequestHandler<PlaceOrderCommand, ErrorOr<PlaceOrderResponse>>
{
    public async Task<ErrorOr<PlaceOrderResponse>> Handle(
        PlaceOrderCommand command,
        CancellationToken ct)
    {
        var recipeIds = command.Items
            .Select(i => i.RecipeId)
            .Distinct()
            .ToArray();

        var recipes = await db.Recipes
            .Where(r => recipeIds.AsEnumerable().Contains(r.Id))
            .Select(r => new
            {
                r.Id,
                r.IngredientsTotalPrice,
                IsBrandActive = r.Brand.IsActive,
                Ingredients = r.RecipeIngredients.Select(ri => new
                {
                    ri.IngredientId,
                    ri.AmountRequired,
                    ri.Ingredient.StockQuantity,
                    ri.Ingredient.Name
                }).ToList()
            })
            .ToArrayAsync(ct);

        var recipesById = recipes.ToDictionary(r => r.Id);

        foreach (var recipeId in recipeIds)
            if (!recipesById.ContainsKey(recipeId))
                return Error.NotFound(
                    "Recipe.NotFound",
                    $"Recipe with ID {recipeId} was not found");

        if (recipes.Any(r => !r.IsBrandActive))
            return Error.Conflict(
                "Order.InactiveBrand",
                "Cannot place an order containing recipes from inactive brands.");

        var requiredIngredients = command.Items
            .SelectMany(item =>
                recipesById[item.RecipeId].Ingredients.Select(ingredient => new
                {
                    ingredient.IngredientId,
                    ingredient.Name,
                    ingredient.StockQuantity,
                    RequiredQuantity = ingredient.AmountRequired * item.Quantity
                }))
            .GroupBy(i => new { i.IngredientId, i.Name })
            .Select(group => new
            {
                group.Key.IngredientId,
                group.Key.Name,
                AvailableQuantity = group.First().StockQuantity,
                RequiredQuantity = group.Sum(i => i.RequiredQuantity)
            })
            .ToArray();
        
        var insufficientIngredient = requiredIngredients
            .FirstOrDefault(i => i.RequiredQuantity > i.AvailableQuantity);

        if (insufficientIngredient is not null)
            return Error.Conflict(
                "Order.InsufficientStock",
                $"""
                 Insufficient stock for ingredient '{insufficientIngredient.Name}'. 
                 Required: {insufficientIngredient.RequiredQuantity}, available: {insufficientIngredient.AvailableQuantity}.
                 """);

        var totalPrice = command.Items.Sum(item =>
            recipesById[item.RecipeId].IngredientsTotalPrice * item.Quantity);

        var order = new Order
        {
            ExternalOrderId = command.ExternalOrderId,
            TotalPrice = totalPrice,
            Source = command.Source
        };

        foreach (var item in command.Items)
        {
            var recipe = recipesById[item.RecipeId];

            order.OrderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                RecipeId = item.RecipeId,
                Quantity = item.Quantity,
                UnitPrice = recipe.IngredientsTotalPrice
            });
        }
        
        var ingredientIds = requiredIngredients
            .Select(i => i.IngredientId)
            .ToArray();

        var ingredients = await db.Ingredients
            .Where(i => ingredientIds.AsEnumerable().Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);

        foreach (var required in requiredIngredients)
        {
            if (!ingredients.TryGetValue(required.IngredientId, out var ingredient))
                return Error.NotFound(
                    "Ingredient.NotFound",
                    $"Ingredient with ID {required.IngredientId} was not found");

            if (ingredient.StockQuantity < required.RequiredQuantity)
                return Error.Conflict(
                    "Order.InsufficientStock",
                    $"Insufficient stock for ingredient '{ingredient.Name}'.");

            ingredient.StockQuantity -= required.RequiredQuantity;
        }

        db.Orders.Add(order);

        await db.SaveChangesAsync(ct);

        return new PlaceOrderResponse(
            order.Id,
            order.Status,
            order.Source,
            order.ExternalOrderId,
            order.TotalPrice,
            order.OrderItems
                .Select(item => new PlaceOrderOrderItemResponseDto(
                    item.RecipeId,
                    item.Quantity,
                    item.UnitPrice))
                .ToList());
    }
}

public static class PlaceOrderEndpoint
{
    public static IEndpointRouteBuilder MapPlaceOrder(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", async (PlaceOrderRequest request, ISender sender, CancellationToken ct) =>
            {
                var command = new PlaceOrderCommand(request.Source, request.ExternalOrderId, request.Items);

                var result = await sender.Send(command, ct);

                return result.ToCreated(response => $"/api/orders/{response.OrderId}");
            })
            .WithName("PlaceOrder")
            .WithSummary("Place an order")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Integration),
                nameof(UserRole.Manager)
            ))
            .Produces<PlaceOrderResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}