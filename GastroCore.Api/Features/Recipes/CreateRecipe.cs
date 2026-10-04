using System.Collections.Immutable;
using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using FluentValidation;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Recipes;

public sealed record CreateRecipeRequest(
    string Name,
    Guid BrandId,
    IReadOnlyList<RecipeIngredientDto> Ingredients);

public sealed record CreateRecipeCommand(
    string Name,
    Guid BrandId,
    IReadOnlyList<RecipeIngredientDto> Ingredients) : IRequest<ErrorOr<CreateRecipeResponse>>;

public sealed record CreateRecipeResponse(
    Guid RecipeId,
    string Name,
    Guid BrandId,
    decimal BasePrice,
    IReadOnlyList<RecipeIngredientDto> Ingredients);

public sealed class CreateRecipeValidator : AbstractValidator<CreateRecipeCommand>
{
    public CreateRecipeValidator()
    {
        RuleFor(r => r.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(500).WithMessage("Name must not exceed 500 characters.");

        RuleFor(r => r.BrandId)
            .NotEmpty().WithMessage("BrandId is required.");

        RuleFor(r => r.Ingredients)
            .NotNull().WithMessage("Ingredients collection cannot be null.")
            .NotEmpty().WithMessage("Ingredients must contain at least one ingredient.")
            .Must(HaveUniqueIngredients).WithMessage("Duplicate ingredients are not allowed in a single recipe.");

        RuleForEach(r => r.Ingredients)
            .SetValidator(new RecipeIngredientDtoValidator());
    }

    private static bool HaveUniqueIngredients(IReadOnlyList<RecipeIngredientDto> ingredients)
    {
        if (ingredients.Count == 0)
            return true;

        return ingredients
            .Select(i => i.IngredientId)
            .Distinct()
            .Count() == ingredients.Count;
    }
}

public sealed class RecipeIngredientDtoValidator : AbstractValidator<RecipeIngredientDto>
{
    public RecipeIngredientDtoValidator()
    {
        RuleFor(i => i.IngredientId)
            .NotEmpty().WithMessage("IngredientId is required.");

        RuleFor(i => i.AmountRequired)
            .GreaterThan(0).WithMessage("AmountRequired must be greater than zero.");
    }
}

public sealed class CreateRecipeHandler(AppDbContext db, ICurrentUserContext userContext)
    : IRequestHandler<CreateRecipeCommand, ErrorOr<CreateRecipeResponse>>
{
    public async Task<ErrorOr<CreateRecipeResponse>> Handle(CreateRecipeCommand command, CancellationToken ct)
    {
        var brandExists = await db.Brands.AnyAsync(b => b.Id == command.BrandId, ct);

        if (!brandExists)
            return Error.NotFound(
                "Brand.NotFound",
                $"Brand with ID '{command.BrandId}' was not found.");

        var ingredientIds = command.Ingredients
            .Select(i => i.IngredientId)
            .ToImmutableArray();

        var ingredientsFromDb = await db.Ingredients
            .Where(i => ingredientIds.Contains(i.Id))
            .Select(i => new { i.Id, i.UnitCost })
            .ToArrayAsync(ct);

        if (ingredientsFromDb.Length != ingredientIds.Length)
            return Error.Validation("Recipe.InvalidIngredients", "One or more provided ingredients do not exist.");

        var ingredientCosts = ingredientsFromDb.ToDictionary(i => i.Id, i => i.UnitCost);

        var calculatedBasePrice = command.Ingredients
            .Sum(i => i.AmountRequired * ingredientCosts[i.IngredientId]);

        var recipe = new Recipe
        {
            Name = command.Name,
            BrandId = command.BrandId,
            BasePrice = calculatedBasePrice,
            CreatedBy = userContext.Id,
            RecipeIngredients = command.Ingredients.Select(i => new RecipeIngredient
            {
                IngredientId = i.IngredientId,
                AmountRequired = i.AmountRequired
            }).ToList()
        };

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync(ct);

        return new CreateRecipeResponse(
            recipe.Id,
            recipe.Name,
            recipe.BrandId,
            recipe.BasePrice,
            command.Ingredients);
    }
}

public static class CreateRecipeEndpoint
{
    public static IEndpointRouteBuilder MapCreateRecipe(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", async (CreateRecipeRequest request, ISender sender, CancellationToken ct) =>
            {
                var command = new CreateRecipeCommand(request.Name, request.BrandId, request.Ingredients);

                var result = await sender.Send(command, ct);

                return result.ToCreated(response => $"/api/recipes/{response.RecipeId}");
            })
            .WithName("CreateRecipe")
            .WithSummary("Creates a new recipe with calculated base price.")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Manager)
            ))
            .Produces<CreateRecipeResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem();

        return app;
    }
}