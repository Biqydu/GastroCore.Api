using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Brands;

public static class GetBrandById
{
    public sealed record Query(Guid Id) : IRequest<ErrorOr<Response>>;

    public sealed record RecipeDto(Guid Id, string Name, decimal IngredientsTotalPrice);

    public sealed record Response(
        Guid Id,
        string Name,
        bool? IsActive,
        IReadOnlyList<RecipeDto> LatestRecipes);

    public sealed class Handler(AppDbContext db, ICurrentUserContext userContext)
        : IRequestHandler<Query, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(Query query, CancellationToken ct)
        {
            var isChefOnly = userContext.IsChefOnly;

            var dbQuery = db.Brands
                .AsNoTracking()
                .Where(b => b.Id == query.Id);

            if (isChefOnly) dbQuery = dbQuery.Where(b => b.IsActive);

            var brand = await dbQuery
                .Select(b =>
                    new Response(b.Id, b.Name, isChefOnly ? null : b.IsActive,
                        b.Recipes
                            .OrderByDescending(r => r.CreatedAt)
                            .ThenByDescending(r => r.Id)
                            .Take(10)
                            .Select(r => new RecipeDto(r.Id, r.Name, r.IngredientsTotalPrice))
                            .ToList())
                )
                .FirstOrDefaultAsync(ct);

            if (brand is null)
                return Error.NotFound(
                    "Brand.NotFound",
                    $"Brand with ID '{query.Id}' was not found.");

            return brand;
        }
    }
}

public static class GetBrandByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetBrandById(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var query = new GetBrandById.Query(id);

                var result = await sender.Send(query, ct);

                return result.ToOk();
            })
            .WithName("GetBrandById")
            .WithSummary("Gets a brand by ID")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Chef),
                nameof(UserRole.Manager)
            ))
            .Produces<GetBrandById.Response>();

        return app;
    }
}