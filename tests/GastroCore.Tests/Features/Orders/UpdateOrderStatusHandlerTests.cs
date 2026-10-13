using ErrorOr;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Features.Orders;
using GastroCore.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace GastroCore.Tests.Features.Orders;

public sealed class UpdateOrderStatusHandlerTests(TestWebAppFactory factory) : IntegrationTestBase(factory)
{
    private const OrderSource Source = OrderSource.Internal;

    [Theory]
    [InlineData(OrderStatus.InPreparation, OrderStatus.Completed)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.InPreparation, OrderStatus.Cancelled)]
    public async Task Handle_AllowedTransition_UpdatesStatus(OrderStatus current, OrderStatus next)
    {
        var order = await SeedOrderAsync(current);

        var result = await Send(new UpdateOrderStatus.Command(order.Id, next));

        result.IsError.ShouldBeFalse();
        (await GetStatusAsync(order.Id)).ShouldBe(next);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Completed)]
    [InlineData(OrderStatus.Completed, OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Completed)]
    public async Task Handle_CompleteFromInvalidStatus_ReturnsConflictAndKeepsStatus(
        OrderStatus current, OrderStatus next)
    {
        var order = await SeedOrderAsync(current);

        var result = await Send(new UpdateOrderStatus.Command(order.Id, next));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe("Order.InvalidStatus");
        (await GetStatusAsync(order.Id)).ShouldBe(current);
    }

    [Theory]
    [InlineData(OrderStatus.Completed, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Cancelled)]
    public async Task Handle_CancelFromInvalidStatus_ReturnsConflictAndKeepsStatus(
        OrderStatus current, OrderStatus next)
    {
        var order = await SeedOrderAsync(current);

        var result = await Send(new UpdateOrderStatus.Command(order.Id, next));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
        result.FirstError.Code.ShouldBe("Order.InvalidStatus");
        (await GetStatusAsync(order.Id)).ShouldBe(current);
    }

    [Fact]
    public async Task Handle_OrderNotFound_ReturnsNotFound()
    {
        var missingId = Guid.NewGuid();

        var result = await Send(new UpdateOrderStatus.Command(missingId, OrderStatus.Completed));

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("Order.NotFound");
        result.FirstError.Description.ShouldContain(missingId.ToString());
    }

    [Fact]
    public async Task Handle_UpdatesOnlyTargetOrder()
    {
        var target = await SeedOrderAsync(OrderStatus.InPreparation, "EXT-A");
        var other = await SeedOrderAsync(OrderStatus.InPreparation, "EXT-B");

        var result = await Send(new UpdateOrderStatus.Command(target.Id, OrderStatus.Completed));

        result.IsError.ShouldBeFalse();
        (await GetStatusAsync(target.Id)).ShouldBe(OrderStatus.Completed);
        (await GetStatusAsync(other.Id)).ShouldBe(OrderStatus.InPreparation);
    }

    private async Task<Order> SeedOrderAsync(OrderStatus status, string externalId = "EXT-1")
    {
        var order = Seed.CreateOrder(Source, externalId, 0m, status);

        await ExecuteDb(async db =>
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        });

        return order;
    }

    private Task<OrderStatus> GetStatusAsync(Guid orderId)
    {
        return QueryDb(db => db.Orders
            .Where(o => o.Id == orderId)
            .Select(o => o.Status)
            .SingleAsync());
    }
}