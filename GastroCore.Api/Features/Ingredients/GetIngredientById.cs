using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using GastroCore.Api.Data;
using GastroCore.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Ingredients;

public sealed record GetIngredientQuery(Guid Id) : IRequest<ErrorOr<GetIngredientResponse>>;

public sealed record GetIngredientResponse(
    Guid Id,
    string Name,
    decimal StockQuantity,
    decimal? UnitCost,
    decimal? MinStockThreshold
);

public sealed class GetIngredientHandler(AppDbContext db, ICurrentUserContext userContext)
    : IRequestHandler<GetIngredientQuery, ErrorOr<GetIngredientResponse>>
{
    public async Task<ErrorOr<GetIngredientResponse>> Handle(GetIngredientQuery query, CancellationToken ct)
    {
        var isChefOnly = userContext.IsInRole(nameof(UserRole.Chef)) &&
                         !userContext.IsInRole(nameof(UserRole.Manager));
        
        var ingredientResponse = await db.Ingredients
            .Where(i => i.Id == query.Id)
            .Select(i => new GetIngredientResponse(
                i.Id,
                i.Name,
                i.StockQuantity,
                isChefOnly ? null : i.UnitCost,
                isChefOnly ? null : i.MinStockThreshold
            ))
            .FirstOrDefaultAsync(ct); 
        
        if (ingredientResponse is null)
        {
            return Error.NotFound(
                code: "Ingredient.NotFound",
                description: $"Ingredient with ID {query.Id} was not found.");
        }

        return ingredientResponse;
    }
}


public static class GetIngredientEndpoint
{
    public static IEndpointRouteBuilder MapGetIngredient(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
            {
                var result = await mediator.Send(new GetIngredientQuery(id), ct);
                return result.ToOk();
            })
            .WithName("GetIngredientById")
            .WithSummary("Gets ingredient by ID.")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Chef),
                nameof(UserRole.Manager)
            ))
            .Produces<GetIngredientResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}