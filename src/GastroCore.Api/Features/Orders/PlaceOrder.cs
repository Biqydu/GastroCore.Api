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
                IsBrandActive = r.Brand.IsActive
            })
            .ToArrayAsync(ct);

        var recipesById = recipes.ToDictionary(r => r.Id);

        foreach (var recipeId in recipeIds)
            if (!recipesById.ContainsKey(recipeId))
                return Error.NotFound(
                    "Recipe.NotFound",
                    $"Recipe with ID {recipeId} was not found");

        if (recipes.Any(r => !r.IsBrandActive))
            return Error.Validation(
                "Order.InactiveBrand",
                "Cannot place an order containing recipes from inactive brands.");

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
                ;
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
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}