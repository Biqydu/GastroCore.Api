using GastroCore.Api.Common;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Ingredients;

public static class GetWarehouseAlerts
{
    public sealed record Query(int PageNumber, int PageSize) : IRequest<PagedResponse<IngredientDto>>;

    public sealed record IngredientDto(
        Guid Id,
        string Name,
        decimal StockQuantity,
        decimal UnitCost,
        decimal MinStockThreshold);

    public sealed class Handler(AppDbContext db) : IRequestHandler<Query, PagedResponse<IngredientDto>>
    {
        public async Task<PagedResponse<IngredientDto>> Handle(Query query, CancellationToken ct)
        {
            var pageNumber = Math.Max(1, query.PageNumber);
            var pageSize = Math.Clamp(query.PageSize, 1, 50);

            var dbQuery = db.Ingredients
                .AsNoTracking()
                .Where(i => i.StockQuantity <= i.MinStockThreshold);

            var totalRecords = await dbQuery.CountAsync(ct);

            var ingredientsBelowThreshold = await dbQuery
                .AsNoTracking()
                .OrderBy(i => i.StockQuantity - i.MinStockThreshold)
                .ThenBy(i => i.Name)
                .ApplyPagination(pageNumber, pageSize)
                .Select(i => new IngredientDto(
                    i.Id,
                    i.Name,
                    i.StockQuantity,
                    i.UnitCost,
                    i.MinStockThreshold
                ))
                .ToArrayAsync(ct);

            return new PagedResponse<IngredientDto>
            {
                Data = ingredientsBelowThreshold,
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize)
            };
        }
    }
}

public static class GetWarehouseAlertsEndpoint
{
    public static IEndpointRouteBuilder MapGetWarehouseAlerts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/alerts", async (
                ISender sender,
                CancellationToken ct,
                int pageNumber = 1,
                int pageSize = 10) =>
            {
                var query = new GetWarehouseAlerts.Query(pageNumber, pageSize);

                var result = await sender.Send(query, ct);

                return result;
            })
            .WithName("Get Warehouse Alerts")
            .WithSummary("Gets all warehouse alerts.")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Manager)
            ))
            .Produces<PagedResponse<GetWarehouseAlerts.IngredientDto>>();

        return app;
    }
}