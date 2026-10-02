using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using FluentValidation;
using GastroCore.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Ingredients;

public sealed record UpdateIngredientRequest(
    string Name,
    decimal StockQuantity,
    decimal UnitCost,
    decimal MinStockThreshold
);

public sealed record UpdateIngredientCommand(
    Guid Id,
    string Name,
    decimal StockQuantity,
    decimal UnitCost,
    decimal MinStockThreshold
) : IRequest<ErrorOr<UpdateIngredientResponse>>;

public sealed record UpdateIngredientResponse(
    Guid Id,
    string Name,
    decimal StockQuantity,
    decimal UnitCost,
    decimal MinStockThreshold);

public sealed class UpdateIngredientValidator : AbstractValidator<UpdateIngredientCommand>
{
    public UpdateIngredientValidator()
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

public sealed class UpdateIngredientHandler(AppDbContext db)
    : IRequestHandler<UpdateIngredientCommand, ErrorOr<UpdateIngredientResponse>>
{
    public async Task<ErrorOr<UpdateIngredientResponse>> Handle(UpdateIngredientCommand command, CancellationToken ct)
    {
        var ingredient = await db.Ingredients.FirstOrDefaultAsync(i => i.Id == command.Id, ct);

        if (ingredient is null)
            return Error.NotFound(
                "Ingredient.NotFound",
                $"Ingredient with ID {command.Id} was not found.");

        ingredient.Name = command.Name;
        ingredient.StockQuantity = command.StockQuantity;
        ingredient.UnitCost = command.UnitCost;
        ingredient.MinStockThreshold = command.MinStockThreshold;

        await db.SaveChangesAsync(ct);

        return new UpdateIngredientResponse(ingredient.Id, ingredient.Name, ingredient.StockQuantity,
            ingredient.UnitCost, ingredient.MinStockThreshold);
    }
}

public static class UpdateIngredientEndpoint
{
    public static IEndpointRouteBuilder MapUpdateIngredient(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{id:guid}",
                async (Guid id, UpdateIngredientRequest request, IMediator mediator, CancellationToken ct) =>
                {
                    var command = new UpdateIngredientCommand(id, request.Name, request.StockQuantity, request.UnitCost,
                        request.MinStockThreshold);

                    var result = await mediator.Send(command, ct);

                    return result.ToOk();
                })
            .WithName("UpdateIngredient")
            .WithSummary("Updates an existing ingredient")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Manager)
            ))
            .Produces<UpdateIngredientResponse>();
        ;

        return app;
    }
}