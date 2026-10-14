using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using FluentValidation;
using GastroCore.Api.Common;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Features.Recipes.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Recipes;

public static class UpdateRecipe
{
    public sealed record Request(string Name, decimal BasePrice, IReadOnlyList<IngredientDto> Ingredients);

    public sealed record IngredientDto(Guid Id, decimal AmountRequired);

    public sealed record Command(Guid RecipeId, string Name, decimal BasePrice, IReadOnlyList<IngredientDto> Ingredients)
        : IRequest<ErrorOr<Response>>;

    public sealed record Response(
        Guid RecipeId,
        string Name,
        Guid BrandId,
        decimal IngredientsTotalPrice,
        IReadOnlyList<IngredientDto> Ingredients);

    public sealed class RecipeValidator : AbstractValidator<Command>
    {
        public RecipeValidator()
        {
            RuleFor(r => r.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(500).WithMessage("Name must not exceed 500 characters.");
            
            RuleFor(r => r.BasePrice)
                .NotEmpty().WithMessage("BasePrice is required.")
                .GreaterThan(0).WithMessage("BasePrice must be greater than zero.");

            RuleFor(r => r.Ingredients)
                .NotNull().WithMessage("Ingredients collection cannot be null.")
                .NotEmpty().WithMessage("Ingredients must contain at least one ingredient.")
                .Must(HaveUniqueIngredients).WithMessage("Duplicate ingredients are not allowed in a single recipe.");

            RuleForEach(r => r.Ingredients)
                .SetValidator(new IngredientValidator());
        }

        private static bool HaveUniqueIngredients(IReadOnlyList<IngredientDto> ingredients)
        {
            if (ingredients.Count == 0)
                return true;

            return ingredients.Select(i => i.Id)
                .Distinct()
                .Count() == ingredients.Count;
        }
    }

    public sealed class IngredientValidator : AbstractValidator<IngredientDto>
    {
        public IngredientValidator()
        {
            RuleFor(i => i.Id)
                .NotEmpty().WithMessage("IngredientId is required.");

            RuleFor(i => i.AmountRequired)
                .GreaterThan(0).WithMessage("AmountRequired must be greater than zero.");
        }
    }

    public sealed class Handler(AppDbContext db)
        : IRequestHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(Command command, CancellationToken ct)
        {
            var recipe = await db.Recipes
                .Include(r => r.RecipeIngredients)
                .FirstOrDefaultAsync(r => r.Id == command.RecipeId, ct);

            if (recipe is null)
                return Error.NotFound(
                    "Recipe.NotFound",
                    $"Recipe with id {command.RecipeId} was not found.");

            var items = command.Ingredients
                .Select(i => (i.Id, i.AmountRequired))
                .ToList();

            var unitCosts = await db.GetUnitCostsAsync(items.Select(i => i.Id), ct);

            var costResult = RecipeCostCalculator.CalculateIngredientsTotalPrice(items, unitCosts);

            if (costResult.IsError)
                return costResult.Errors;

            recipe.Name = command.Name;
            recipe.BasePrice = command.BasePrice;
            recipe.IngredientsTotalPrice = costResult.Value;

            recipe.RecipeIngredients.Clear();
            foreach (var ingredientDto in command.Ingredients)
                recipe.RecipeIngredients.Add(new RecipeIngredient
                {
                    RecipeId = recipe.Id,
                    IngredientId = ingredientDto.Id,
                    AmountRequired = ingredientDto.AmountRequired
                });

            await db.SaveChangesAsync(ct);

            return new Response(
                recipe.Id,
                recipe.Name,
                recipe.BrandId,
                recipe.IngredientsTotalPrice,
                command.Ingredients);
        }
    }
}

public static class UpdateRecipeEndpoint
{
    public static IEndpointRouteBuilder MapUpdateRecipe(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{id:guid}", async (Guid id, UpdateRecipe.Request request, ISender sender, CancellationToken ct) =>
            {
                var command = new UpdateRecipe.Command(id, request.Name, request.BasePrice, request.Ingredients);

                var result = await sender.Send(command, ct);

                return result.ToOk();
            })
            .WithName("UpdateRecipe")
            .WithSummary("Updates a recipe")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Manager)
            ))
            .Produces<UpdateRecipe.Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}