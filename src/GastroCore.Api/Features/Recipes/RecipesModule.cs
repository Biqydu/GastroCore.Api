namespace GastroCore.Api.Features.Recipes;

public static class RecipesModule
{
    public static IEndpointRouteBuilder MapRecipesModule(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/recipes")
            .WithTags("Recipes")
            .MapCreateRecipe()
            .MapGetRecipes()
            .MapGetRecipeById()
            .MapUpdateRecipe();

        app.MapGroup("/brands")
            .WithTags("Recipes")
            .MapGetRecipesByBrand();

        return app;
    }
}