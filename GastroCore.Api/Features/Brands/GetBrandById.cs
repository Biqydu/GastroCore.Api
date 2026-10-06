using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Brands;

public sealed record GetBrandByIdQuery(Guid Id) : IRequest<ErrorOr<GetBrandByIdResponse>>;

public sealed record GetBrandByIdRecipeDto(Guid Id, string Name, decimal BasePrice);

public sealed record GetBrandByIdResponse(
    Guid Id,
    string Name,
    bool? IsActive,
    IReadOnlyList<GetBrandByIdRecipeDto> LatestRecipes);

public sealed class GetBrandByIdHandler(AppDbContext db, ICurrentUserContext userContext)
    : IRequestHandler<GetBrandByIdQuery, ErrorOr<GetBrandByIdResponse>>
{
    public async Task<ErrorOr<GetBrandByIdResponse>> Handle(GetBrandByIdQuery query, CancellationToken ct)
    {
        var isChefOnly = userContext.IsInRole(nameof(UserRole.Chef)) &&
                         !userContext.IsInRole(nameof(UserRole.Manager));

        var dbQuery = db.Brands
            .AsNoTracking()
            .Where(b => b.Id == query.Id);

        if (isChefOnly) dbQuery = dbQuery.Where(b => b.IsActive);

        var brand = await dbQuery
            .Select(b =>
                new GetBrandByIdResponse(b.Id, b.Name, isChefOnly ? null : b.IsActive,
                    b.Recipes
                        .OrderByDescending(r => r.CreatedAt)
                        .ThenByDescending(r => r.Id)
                        .Take(10)
                        .Select(r => new GetBrandByIdRecipeDto(r.Id, r.Name, r.BasePrice))
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

public static class GetBrandByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetBrandById(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var query = new GetBrandByIdQuery(id);

                var result = await sender.Send(query, ct);

                return result.ToOk();
            })
            .WithName("GetBrandById")
            .WithSummary("Gets a brand by ID")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Chef),
                nameof(UserRole.Manager)
            ))
            .Produces<GetBrandByIdResponse>();
        ;

        return app;
    }
}