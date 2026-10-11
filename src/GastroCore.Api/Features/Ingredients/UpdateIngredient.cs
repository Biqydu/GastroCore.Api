using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using FluentValidation;
using GastroCore.Api.Common;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Features.Recipes.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Ingredients;

public static class UpdateIngredient
{
    public sealed record Request(
        string Name,
        decimal StockQuantity,
        decimal UnitCost,
        decimal MinStockThreshold
    );

    public sealed record Command(
        Guid Id,
        string Name,
        decimal StockQuantity,
        decimal UnitCost,
        decimal MinStockThreshold
    ) : IRequest<ErrorOr<Response>>;

    public sealed record Response(
        Guid Id,
        string Name,
        decimal StockQuantity,
        decimal UnitCost,
        decimal MinStockThreshold);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Name)
                .NotEmpty().WithMessage("Name is required")
                .MaximumLength(255).WithMessage("Name must not exceed 255 characters");

            RuleFor(c => c.StockQuantity)
                .NotEmpty().WithMessage("StockQuantity is required")
                .GreaterThan(0).WithMessage("StockQuantity must be greater than 0");

            RuleFor(c => c.UnitCost)
                .NotEmpty().WithMessage("UnitCost is required")
                .GreaterThan(0).WithMessage("UnitCost must be greater than 0");

            RuleFor(c => c.MinStockThreshold)
                .NotEmpty().WithMessage("MinStockThreshold is required")
                .GreaterThanOrEqualTo(0).WithMessage("MinStockThreshold must be greater than or equal to 0");
        }
    }

    public sealed class Handler(AppDbContext db)
        : IRequestHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(Command command, CancellationToken ct)
        {
            var ingredient = await db.Ingredients
                .FirstOrDefaultAsync(i => i.Id == command.Id, ct);

            if (ingredient is null)
                return Error.NotFound(
                    "Ingredient.NotFound",
                    $"Ingredient with ID {command.Id} was not found.");

            if (ingredient.UnitCost != command.UnitCost)
            {
                var recipes = await db.Recipes
                    .Include(r => r.RecipeIngredients)
                    .Where(r => r.RecipeIngredients.Any(ri => ri.IngredientId == ingredient.Id))
                    .ToArrayAsync(ct);

                if (recipes.Length > 0)
                {
                    var ingredientIds = recipes
                        .SelectMany(r => r.RecipeIngredients)
                        .Select(ri => ri.IngredientId);

                    var unitCosts = await db.GetUnitCostsAsync(ingredientIds, ct);

                    unitCosts[ingredient.Id] = command.UnitCost;

                    foreach (var recipe in recipes)
                    {
                        var items = recipe.RecipeIngredients
                            .Select(ri => (ri.IngredientId, ri.AmountRequired));

                        var costResult = RecipeCostCalculator.CalculateIngredientsTotalPrice(items, unitCosts);

                        if (costResult.IsError)
                            return costResult.Errors;

                        recipe.IngredientsTotalPrice = costResult.Value;
                    }
                }
            }

            ingredient.Name = command.Name;
            ingredient.StockQuantity = command.StockQuantity;
            ingredient.UnitCost = command.UnitCost;
            ingredient.MinStockThreshold = command.MinStockThreshold;

            await db.SaveChangesAsync(ct);

            return new Response(ingredient.Id, ingredient.Name, ingredient.StockQuantity,
                ingredient.UnitCost, ingredient.MinStockThreshold);
        }
    }
}

public static class UpdateIngredientEndpoint
{
    public static IEndpointRouteBuilder MapUpdateIngredient(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{id:guid}",
                async (Guid id, UpdateIngredient.Request request, ISender sender, CancellationToken ct) =>
                {
                    var command = new UpdateIngredient.Command(id, request.Name, request.StockQuantity,
                        request.UnitCost,
                        request.MinStockThreshold);

                    var result = await sender.Send(command, ct);

                    return result.ToOk();
                })
            .WithName("UpdateIngredient")
            .WithSummary("Updates an existing ingredient")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Manager)
            ))
            .Produces<UpdateIngredient.Response>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}