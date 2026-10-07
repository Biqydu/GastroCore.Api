using GastroCore.Api.Common;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Recipes;

public sealed record GetRecipesQuery(int PageNumber, int PageSize) : IRequest<PagedResponse<GetRecipesDto>>;

public sealed record GetRecipesDto(
    Guid RecipeId,
    string RecipeName,
    Guid BrandId,
    string BrandName,
    decimal? BasePrice,
    bool? IsActive);

public sealed class GetRecipesHandler(AppDbContext db, ICurrentUserContext userContext)
    : IRequestHandler<GetRecipesQuery, PagedResponse<GetRecipesDto>>
{
    public async Task<PagedResponse<GetRecipesDto>> Handle(GetRecipesQuery query,
        CancellationToken ct)
    {
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        var isChefOnly = userContext.IsChefOnly;
        var dbQuery = db.Recipes.AsNoTracking();

        if (isChefOnly) dbQuery = dbQuery.Where(r => r.Brand.IsActive);

        var totalRecords = await dbQuery.CountAsync(ct);

        var recipes = await dbQuery
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .ApplyPagination(pageNumber, pageSize)
            .Select(r => new GetRecipesDto(r.Id, r.Name, r.Brand.Id, r.Brand.Name, isChefOnly ? null : r.BasePrice,
                isChefOnly ? null : r.Brand.IsActive))
            .ToArrayAsync(ct);

        return new PagedResponse<GetRecipesDto>
        {
            Data = recipes,
            TotalRecords = totalRecords,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize)
        };
    }
}

public static class GetRecipesEndpoint
{
    public static IEndpointRouteBuilder MapGetRecipes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", async (
                ISender sender,
                CancellationToken ct,
                int pageNumber = 1,
                int pageSize = 10) =>
            {
                var query = new GetRecipesQuery(pageNumber, pageSize);

                var result = await sender.Send(query, ct);

                return Results.Ok(result);
            })
            .WithName("GetRecipes")
            .WithSummary("Gets recipes")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Chef),
                nameof(UserRole.Manager)
            ))
            .Produces<PagedResponse<GetRecipesDto>>();
        ;

        return app;
    }
}