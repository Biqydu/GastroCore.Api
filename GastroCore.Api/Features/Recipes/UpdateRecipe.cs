using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using FluentValidation;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Features.Recipes.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Recipes;

public sealed record UpdateRecipeRequest(string Name, IReadOnlyList<UpdateRecipeIngredientRequest> Ingredients);
public sealed record UpdateRecipeIngredientRequest(Guid Id, decimal AmountRequired);

public sealed record UpdateRecipeCommand(Guid RecipeId, string Name, IReadOnlyList<UpdateRecipeIngredientRequest> Ingredients) 
    : IRequest<ErrorOr<UpdateRecipeResponse>>;

public sealed record UpdateRecipeResponse(
    Guid RecipeId,
    string Name,
    Guid BrandId,
    decimal BasePrice,
    IReadOnlyList<UpdateRecipeIngredientRequest> Ingredients);

public sealed class UpdateRecipeValidator : AbstractValidator<UpdateRecipeCommand>
{
    public UpdateRecipeValidator()
    {
        RuleFor(r => r.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(500).WithMessage("Name must not exceed 500 characters.");

        RuleFor(r => r.Ingredients)
            .NotNull().WithMessage("Ingredients collection cannot be null.")
            .NotEmpty().WithMessage("Ingredients must contain at least one ingredient.")
            .Must(HaveUniqueIngredients).WithMessage("Duplicate ingredients are not allowed in a single recipe.");

        RuleForEach(r => r.Ingredients)
            .SetValidator(new UpdateRecipeIngredientRequestValidator());
    }

    private static bool HaveUniqueIngredients(IReadOnlyList<UpdateRecipeIngredientRequest> ingredients)
    {
        if (ingredients.Count == 0)
            return true;

        return ingredients.Select(i => i.Id)
            .Distinct()
            .Count() == ingredients.Count;
    }
}

public sealed class UpdateRecipeIngredientRequestValidator : AbstractValidator<UpdateRecipeIngredientRequest>
{
    public UpdateRecipeIngredientRequestValidator()
    {
        RuleFor(i => i.Id)
            .NotEmpty().WithMessage("IngredientId is required.");

        RuleFor(i => i.AmountRequired)
            .GreaterThan(0).WithMessage("AmountRequired must be greater than zero.");
    }
}

public sealed class UpdateRecipeHandler(AppDbContext db, IRecipeCostCalculator costCalculator) 
    : IRequestHandler<UpdateRecipeCommand, ErrorOr<UpdateRecipeResponse>>
{
    public async Task<ErrorOr<UpdateRecipeResponse>> Handle(UpdateRecipeCommand command, CancellationToken ct)
    {
        var recipe = await db.Recipes
            .Include(r => r.RecipeIngredients)
            .FirstOrDefaultAsync(r => r.Id == command.RecipeId, ct);

        if (recipe is null)
            return Error.NotFound(
                code: "Recipe.NotFound",
                description: $"Recipe with id {command.RecipeId} was not found.");

        var items = command.Ingredients
            .Select(i => (i.Id, i.AmountRequired))
            .ToList();

        var costResult = await costCalculator.CalculateBasePriceAsync(items, ct);

        if (costResult.IsError)
            return costResult.Errors;

        recipe.Name = command.Name;
        recipe.BasePrice = costResult.Value;

        recipe.RecipeIngredients.Clear();
        foreach (var ingredientDto in command.Ingredients)
        {
            recipe.RecipeIngredients.Add(new RecipeIngredient
            {
                RecipeId = recipe.Id,
                IngredientId = ingredientDto.Id,
                AmountRequired = ingredientDto.AmountRequired
            });
        }

        await db.SaveChangesAsync(ct);

        return new UpdateRecipeResponse(
            recipe.Id,
            recipe.Name,
            recipe.BrandId,
            recipe.BasePrice,
            command.Ingredients);
    }
}

public static class UpdateRecipeEndpoint
{
    public static IEndpointRouteBuilder MapUpdateRecipe(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{id:guid}", async (Guid id, UpdateRecipeRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateRecipeCommand(id, request.Name, request.Ingredients);

            var result = await sender.Send(command, ct);

            return result.ToOk();
        })
        .WithName("UpdateRecipe")
        .WithSummary("Updates a recipe")
        .RequireAuthorization(policy => policy.RequireRole(
            nameof(UserRole.Manager)
        ))
        .Produces<UpdateRecipeResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}