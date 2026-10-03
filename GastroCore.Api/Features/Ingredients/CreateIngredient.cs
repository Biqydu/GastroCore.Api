using FluentValidation;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Services;
using MediatR;

namespace GastroCore.Api.Features.Ingredients;

public sealed record CreateIngredientRequest(
    string Name,
    decimal StockQuantity,
    decimal UnitCost,
    decimal MinStockThreshold
);

public sealed record CreateIngredientCommand(
    string Name,
    decimal StockQuantity,
    decimal UnitCost,
    decimal MinStockThreshold
) : IRequest<CreateIngredientResponse>;

public sealed record CreateIngredientResponse(
    Guid Id,
    string Name,
    decimal StockQuantity,
    decimal UnitCost,
    decimal MinStockThreshold);

public sealed class CreateIngredientValidator : AbstractValidator<CreateIngredientCommand>
{
    public CreateIngredientValidator()
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

public sealed class CreateIngredientHandler(AppDbContext db, ICurrentUserContext userContext)
    : IRequestHandler<CreateIngredientCommand, CreateIngredientResponse>
{
    public async Task<CreateIngredientResponse> Handle(CreateIngredientCommand command, CancellationToken ct)
    {
        var ingredient = new Ingredient
        {
            Name = command.Name,
            StockQuantity = command.StockQuantity,
            UnitCost = command.UnitCost,
            MinStockThreshold = command.MinStockThreshold,
            CreatedBy = userContext.Id
        };

        db.Ingredients.Add(ingredient);

        await db.SaveChangesAsync(ct);

        return new CreateIngredientResponse(ingredient.Id, ingredient.Name, ingredient.StockQuantity,
            ingredient.UnitCost, ingredient.MinStockThreshold);
    }
}

public static class CreateIngredientEndpoint
{
    public static IEndpointRouteBuilder MapCreateIngredient(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", async (
                CreateIngredientRequest request,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var command = new CreateIngredientCommand(
                    request.Name,
                    request.StockQuantity,
                    request.UnitCost,
                    request.MinStockThreshold
                );

                var response = await mediator.Send(command, ct);

                return Results.Created($"/api/ingredients/{response.Id}", response);
            })
            .WithName("CreateIngredient")
            .WithSummary("Adds a new ingredient to the warehouse.")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Manager)
            ))
            .Produces<CreateIngredientResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        return app;
    }
}