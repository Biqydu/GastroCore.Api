using GastroCore.Api.Common;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Orders;

public static class GetActiveOrders
{
    public sealed record Query(int PageNumber, int PageSize) : IRequest<PagedResponse<OrderResponseDto>>;

    public sealed record OrderItemResponseDto(Guid RecipeId, int Quantity, decimal? UnitPrice);

    public sealed record OrderResponseDto(
        Guid OrderId,
        OrderStatus Status,
        OrderSource Source,
        string ExternalOrderId,
        decimal? TotalPrice,
        IReadOnlyList<OrderItemResponseDto> OrderItems);

    public sealed class Handler(AppDbContext db, ICurrentUserContext userContext)
        : IRequestHandler<Query, PagedResponse<OrderResponseDto>>
    {
        public async Task<PagedResponse<OrderResponseDto>> Handle(Query query, CancellationToken ct)
        {
            var pageNumber = Math.Max(1, query.PageNumber);
            var pageSize = Math.Clamp(query.PageSize, 1, 50);

            var dbQuery = db.Orders
                .AsNoTracking()
                .Where(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.InPreparation);

            var totalRecords = await dbQuery.CountAsync(ct);

            var isChefOnly = userContext.IsChefOnly;
            
            var orders = await dbQuery
                .OrderBy(o => o.Status)
                .ThenBy(o => o.CreatedAt)
                .ThenBy(o => o.Id)
                .ApplyPagination(pageNumber, pageSize)
                .Select(o => new OrderResponseDto(o.Id, o.Status, o.Source, o.ExternalOrderId,
                    isChefOnly ? null : o.TotalPrice,
                    o.OrderItems.Select(item =>
                            new OrderItemResponseDto(item.RecipeId, item.Quantity, isChefOnly ? null : item.UnitPrice))
                        .ToArray()))
                .ToArrayAsync(ct);

            return new PagedResponse<OrderResponseDto>
            {
                Data = orders,
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize)
            };
        }
    }
}

public static class GetActiveOrdersEndpoint
{
    public static IEndpointRouteBuilder MapGetActiveOrders(this IEndpointRouteBuilder app)
    {
        app.MapGet("/active", async (
                ISender sender,
                CancellationToken ct,
                int pageNumber = 1,
                int pageSize = 10) =>
            {
                var query = new GetActiveOrders.Query(pageNumber, pageSize);

                var result = await sender.Send(query, ct);

                return Results.Ok(result);
            })
            .WithName("GetActiveOrders")
            .WithSummary("Get active orders")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Chef),
                nameof(UserRole.Manager)
            ))
            .Produces<PagedResponse<GetActiveOrders.OrderResponseDto>>();

        return app;
    }
}