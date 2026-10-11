using GastroCore.Api.Common;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Ingredients;

public static class GetIngredients
{
    public sealed record Query(int PageNumber, int PageSize) : IRequest<PagedResponse<IngredientDto>>;

    public sealed record IngredientDto(
        Guid Id,
        string Name,
        decimal StockQuantity,
        decimal? UnitCost,
        decimal? MinStockThreshold);

    public sealed class Handler(AppDbContext db, ICurrentUserContext userContext)
        : IRequestHandler<Query, PagedResponse<IngredientDto>>
    {
        public async Task<PagedResponse<IngredientDto>> Handle(Query query, CancellationToken ct)
        {
            var pageNumber = Math.Max(1, query.PageNumber);
            var pageSize = Math.Clamp(query.PageSize, 1, 50);

            var totalRecords = await db.Ingredients.CountAsync(ct);

            var isChefOnly = userContext.IsChefOnly;

            var ingredients = await db.Ingredients
                .AsNoTracking()
                .OrderByDescending(i => i.StockQuantity)
                .ThenByDescending(i => i.CreatedAt)
                .ApplyPagination(pageNumber, pageSize)
                .Select(i => new IngredientDto(
                    i.Id,
                    i.Name,
                    i.StockQuantity,
                    isChefOnly ? null : i.UnitCost,
                    isChefOnly ? null : i.MinStockThreshold
                ))
                .ToArrayAsync(ct);

            return new PagedResponse<IngredientDto>
            {
                Data = ingredients,
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize)
            };
        }
    }
}

public static class GetIngredientsEndpoint
{
    public static IEndpointRouteBuilder MapGetIngredients(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", async (
                ISender sender,
                CancellationToken ct,
                int pageNumber = 1,
                int pageSize = 10) =>
            {
                var query = new GetIngredients.Query(pageNumber, pageSize);

                var result = await sender.Send(query, ct);

                return Results.Ok(result);
            })
            .WithName("GetIngredients")
            .WithSummary("Gets ingredients.")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Chef),
                nameof(UserRole.Manager)
            ))
            .Produces<PagedResponse<GetIngredients.IngredientDto>>();

        return app;
    }
}