namespace GastroCore.Api.Features.Brands;

public static class BrandsModule
{
    public static IEndpointRouteBuilder MapBrandsModule(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/brands")
            .WithTags("Brands")
            .MapCreateBrand()
            .MapToggleBrandStatus()
            .MapGetBrands()
            .MapGetBrandById();

        return app;
    }
}