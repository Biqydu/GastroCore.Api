using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Ingredients;

public static class GetIngredientById
{
    public sealed record Query(Guid Id) : IRequest<ErrorOr<Response>>;

    public sealed record Response(
        Guid Id,
        string Name,
        decimal StockQuantity,
        decimal? UnitCost,
        decimal? MinStockThreshold
    );

    public sealed class Handler(AppDbContext db, ICurrentUserContext userContext)
        : IRequestHandler<Query, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(Query query, CancellationToken ct)
        {
            var isChefOnly = userContext.IsChefOnly;

            var ingredientResponse = await db.Ingredients
                .Where(i => i.Id == query.Id)
                .Select(i => new Response(
                    i.Id,
                    i.Name,
                    i.StockQuantity,
                    isChefOnly ? null : i.UnitCost,
                    isChefOnly ? null : i.MinStockThreshold
                ))
                .FirstOrDefaultAsync(ct);

            if (ingredientResponse is null)
                return Error.NotFound(
                    "Ingredient.NotFound",
                    $"Ingredient with ID {query.Id} was not found.");

            return ingredientResponse;
        }
    }
}

public static class GetIngredientEndpoint
{
    public static IEndpointRouteBuilder MapGetIngredient(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetIngredientById.Query(id), ct);
                return result.ToOk();
            })
            .WithName("GetIngredientById")
            .WithSummary("Gets ingredient by ID.")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Chef),
                nameof(UserRole.Manager)
            ))
            .Produces<GetIngredientById.Response>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}