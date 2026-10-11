using FluentValidation;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Services;
using MediatR;

namespace GastroCore.Api.Features.Ingredients;

public static class CreateIngredient
{
    public sealed record Request(
        string Name,
        decimal StockQuantity,
        decimal UnitCost,
        decimal MinStockThreshold
    );

    public sealed record Command(
        string Name,
        decimal StockQuantity,
        decimal UnitCost,
        decimal MinStockThreshold
    ) : IRequest<Response>;

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

    public sealed class Handler(AppDbContext db, ICurrentUserContext userContext)
        : IRequestHandler<Command, Response>
    {
        public async Task<Response> Handle(Command command, CancellationToken ct)
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

            return new Response(ingredient.Id, ingredient.Name, ingredient.StockQuantity,
                ingredient.UnitCost, ingredient.MinStockThreshold);
        }
    }
}

public static class CreateIngredientEndpoint
{
    public static IEndpointRouteBuilder MapCreateIngredient(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", async (
                CreateIngredient.Request request,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new CreateIngredient.Command(
                    request.Name,
                    request.StockQuantity,
                    request.UnitCost,
                    request.MinStockThreshold
                );

                var response = await sender.Send(command, ct);

                return Results.Created($"/api/ingredients/{response.Id}", response);
            })
            .WithName("CreateIngredient")
            .WithSummary("Adds a new ingredient to the warehouse.")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Manager)
            ))
            .Produces<CreateIngredient.Response>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}