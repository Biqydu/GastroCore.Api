namespace GastroCore.Api.Features.Recipes;

public static class RecipesModule
{
    public static IEndpointRouteBuilder MapRecipesModule(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/recipes")
            .WithTags("Recipes")
            .MapCreateRecipe();
        
        return app;
    }
}