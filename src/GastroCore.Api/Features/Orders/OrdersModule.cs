namespace GastroCore.Api.Features.Orders;

public static class OrdersModule
{
    public static IEndpointRouteBuilder MapOrdersModule(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/orders")
            .WithTags("Orders")
            .MapPlaceOrder();

        return app;
    }
}