using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using FluentValidation;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Features.Recipes.Services;
using GastroCore.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Recipes;

public sealed record CreateRecipeRequest(
    string Name,
    Guid BrandId,
    IReadOnlyList<CreateRecipeIngredientDto> Ingredients);
public sealed record CreateRecipeIngredientDto(Guid Id, decimal AmountRequired);

public sealed record CreateRecipeCommand(
    string Name,
    Guid BrandId,
    IReadOnlyList<CreateRecipeIngredientDto> Ingredients) : IRequest<ErrorOr<CreateRecipeResponse>>;

public sealed record CreateRecipeResponse(
    Guid RecipeId,
    string Name,
    Guid BrandId,
    decimal BasePrice,
    IReadOnlyList<CreateRecipeIngredientDto> Ingredients);

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

    private static bool HaveUniqueIngredients(IReadOnlyList<CreateRecipeIngredientDto> ingredients)
    {
        if (ingredients.Count == 0)
            return true;

        return ingredients
            .Select(i => i.Id)
            .Distinct()
            .Count() == ingredients.Count;
    }
}

public sealed class RecipeIngredientDtoValidator : AbstractValidator<CreateRecipeIngredientDto>
{
    public RecipeIngredientDtoValidator()
    {
        RuleFor(i => i.Id)
            .NotEmpty().WithMessage("IngredientId is required.");

        RuleFor(i => i.AmountRequired)
            .GreaterThan(0).WithMessage("AmountRequired must be greater than zero.");
    }
}

public sealed class CreateRecipeHandler(
    AppDbContext db,
    ICurrentUserContext userContext,
    IRecipeCostCalculator costCalculator)
    : IRequestHandler<CreateRecipeCommand, ErrorOr<CreateRecipeResponse>>
{
    public async Task<ErrorOr<CreateRecipeResponse>> Handle(CreateRecipeCommand command, CancellationToken ct)
    {
        var brandExists = await db.Brands.AnyAsync(b => b.Id == command.BrandId, ct);

        if (!brandExists)
            return Error.NotFound(
                "Brand.NotFound",
                $"Brand with ID '{command.BrandId}' was not found.");

        var items = command.Ingredients
            .Select(i => (i.Id, i.AmountRequired))
            .ToArray();

        var costResult = await costCalculator.CalculateBasePriceAsync(items, ct);

        if (costResult.IsError)
            return costResult.Errors;

        var recipe = new Recipe
        {
            Name = command.Name,
            BrandId = command.BrandId,
            BasePrice = costResult.Value,
            CreatedBy = userContext.Id,
            RecipeIngredients = command.Ingredients.Select(i => new RecipeIngredient
            {
                IngredientId = i.Id,
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
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}