using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Recipes;

public sealed record GetRecipeByIdQuery(Guid Id) : IRequest<ErrorOr<GetRecipeByIdResponse>>;

public sealed record GetRecipeByIdIngredientDetailsDto(Guid Id, string Name, decimal StockQuantity, decimal AmountRequired);

public sealed record GetRecipeByIdResponse(
    Guid RecipeId,
    string RecipeName,
    Guid BrandId,
    string BrandName,
    decimal? IngredientsTotalPrice,
    bool? IsActive,
    IReadOnlyList<GetRecipeByIdIngredientDetailsDto> Ingredients);

public sealed class GetRecipeByIdHandler(AppDbContext db, ICurrentUserContext userContext)
    : IRequestHandler<GetRecipeByIdQuery, ErrorOr<GetRecipeByIdResponse>>
{
    public async Task<ErrorOr<GetRecipeByIdResponse>> Handle(GetRecipeByIdQuery query, CancellationToken ct)
    {
        var isChefOnly = userContext.IsChefOnly;

        var dbQuery = db.Recipes
            .AsNoTracking()
            .Where(recipe => recipe.Id == query.Id);

        if (isChefOnly) dbQuery = dbQuery.Where(r => r.Brand.IsActive);

        var recipe = await dbQuery
            .Select(r => new GetRecipeByIdResponse(
                r.Id,
                r.Name,
                r.BrandId,
                r.Brand.Name,
                isChefOnly ? null : r.IngredientsTotalPrice,
                isChefOnly ? null : r.Brand.IsActive,
                r.RecipeIngredients
                    .OrderBy(ri => ri.Ingredient.Name)
                    .Select(ri => new GetRecipeByIdIngredientDetailsDto(ri.IngredientId, ri.Ingredient.Name,
                        ri.Ingredient.StockQuantity, ri.AmountRequired))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

        if (recipe is null)
            return Error.NotFound("Recipe.NotFound", $"Recipe with ID {query.Id} was not found");

        return recipe;
    }
}

public static class GetRecipeByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetRecipeById(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var query = new GetRecipeByIdQuery(id);

                var result = await sender.Send(query, ct);

                return result.ToOk();
            })
            .WithName("GetRecipeById")
            .WithSummary("Get a recipe by ID")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Chef),
                nameof(UserRole.Manager)
            ))
            .Produces<GetRecipeByIdResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}