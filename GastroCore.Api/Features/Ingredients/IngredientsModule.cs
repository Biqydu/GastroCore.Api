namespace GastroCore.Api.Features.Ingredients;

public static class IngredientsModule
{
    public static IEndpointRouteBuilder MapIngredientsModule(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/ingredients")
            .WithTags("Ingredients")
            .MapCreateIngredient()
            .MapGetIngredient()
            .MapGetIngredients()
            .MapUpdateIngredient();

        return app;
    }
}