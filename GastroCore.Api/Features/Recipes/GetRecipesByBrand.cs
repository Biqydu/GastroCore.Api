using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using GastroCore.Api.Common;
using GastroCore.Api.Data;
using GastroCore.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Recipes;

public sealed record GetRecipesByBrandQuery(Guid BrandId, int PageNumber, int PageSize)
    : IRequest<ErrorOr<PagedResponse<GetRecipesByBrandDto>>>;

public sealed record GetRecipesByBrandDto(
    Guid Id,
    string Name,
    decimal? BasePrice);

public sealed class GetRecipesByBrandHandler(AppDbContext db, ICurrentUserContext userContext)
    : IRequestHandler<GetRecipesByBrandQuery, ErrorOr<PagedResponse<GetRecipesByBrandDto>>>
{
    public async Task<ErrorOr<PagedResponse<GetRecipesByBrandDto>>> Handle(GetRecipesByBrandQuery query,
        CancellationToken ct)
    {
        var brandExists = await db.Brands
            .AnyAsync(b => b.Id == query.BrandId, ct);

        if (!brandExists)
            return Error.NotFound(
                "Brand.NotFound",
                $"Brand with ID '{query.BrandId}' was not found.");

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        var isChefOnly = userContext.IsInRole(nameof(UserRole.Chef)) &&
                         !userContext.IsInRole(nameof(UserRole.Manager));

        var dbQuery = db.Recipes
            .AsNoTracking()
            .Where(r => r.BrandId == query.BrandId);

        if (isChefOnly) dbQuery = dbQuery.Where(r => r.Brand.IsActive);

        var totalRecords = await dbQuery.CountAsync(ct);

        var recipes = await dbQuery
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .ApplyPagination(pageNumber, pageSize)
            .Select(r => new GetRecipesByBrandDto(r.Id, r.Name, isChefOnly ? null : r.BasePrice))
            .ToArrayAsync(ct);

        return new PagedResponse<GetRecipesByBrandDto>
        {
            Data = recipes,
            TotalRecords = totalRecords,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize)
        };
    }
}

public static class GetRecipesByBrandEndpoint
{
    public static IEndpointRouteBuilder MapGetRecipesByBrand(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{brandId:guid}/recipes", async (Guid brandId, ISender sender, CancellationToken ct,
            int pageNumber = 1,
            int pageSize = 10) =>
        {
            var query = new GetRecipesByBrandQuery(brandId, pageNumber, pageSize);

            var result = await sender.Send(query, ct);

            return result.ToOk();
        })
        .WithName("GetRecipesByBrand")
        .WithSummary("Get recipes by brand")
        .RequireAuthorization(policy => policy.RequireRole(
            nameof(UserRole.Chef),
            nameof(UserRole.Manager)
        ))
        .Produces<PagedResponse<GetRecipesByBrandDto>>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}