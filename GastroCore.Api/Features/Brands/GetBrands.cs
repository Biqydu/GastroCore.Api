using GastroCore.Api.Common;
using GastroCore.Api.Data;
using GastroCore.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Brands;

public sealed record GetBrandsQuery(int PageNumber, int PageSize) : IRequest<PagedResponse<GetBrandsDto>>;

public sealed record GetBrandsDto(Guid Id, string Name, bool? IsActive);

public sealed class GetBrandsHandler(AppDbContext db, ICurrentUserContext userContext)
    : IRequestHandler<GetBrandsQuery, PagedResponse<GetBrandsDto>>
{
    public async Task<PagedResponse<GetBrandsDto>> Handle(GetBrandsQuery query, CancellationToken ct)
    {
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        var isChefOnly = userContext.IsInRole(nameof(UserRole.Chef)) &&
                         !userContext.IsInRole(nameof(UserRole.Manager));

        var dbQuery = db.Brands.AsNoTracking();

        if (isChefOnly) dbQuery = dbQuery.Where(b => b.IsActive);

        var totalRecords = await dbQuery.CountAsync(ct);

        var brands = await dbQuery
            .OrderByDescending(b => b.Recipes.Count)
            .ThenByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .ApplyPagination(pageNumber, pageSize)
            .Select(b => new GetBrandsDto(b.Id, b.Name, isChefOnly ? null : b.IsActive))
            .ToArrayAsync(ct);

        return new PagedResponse<GetBrandsDto>
        {
            Data = brands,
            TotalRecords = totalRecords,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize)
        };
    }
}

public static class GetBrandsEndpoint
{
    public static IEndpointRouteBuilder MapGetBrands(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", async (
                ISender sender,
                CancellationToken ct,
                int pageNumber = 1,
                int pageSize = 10) =>
            {
                var query = new GetBrandsQuery(pageNumber, pageSize);

                var result = await sender.Send(query, ct);

                return Results.Ok(result);
            })
            .WithName("GetBrands")
            .WithSummary("Gets all brands.")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Chef),
                nameof(UserRole.Manager)
            ))
            .Produces<PagedResponse<GetBrandsDto>>();

        return app;
    }
}