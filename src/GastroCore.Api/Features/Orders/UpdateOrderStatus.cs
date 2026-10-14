using System.Collections.ObjectModel;
using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using FluentValidation;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Orders;

public static class UpdateOrderStatus
{
    public sealed record Request(OrderStatus NewStatus);

    public sealed record Command(Guid OrderId, OrderStatus NewStatus) : IRequest<ErrorOr<Success>>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OrderId)
                .NotEmpty().WithMessage("OrderId is required.");

            RuleFor(x => x.NewStatus)
                .IsInEnum().WithMessage("NewStatus is invalid.");
        }
    }

    public sealed class Handler(AppDbContext db) : IRequestHandler<Command, ErrorOr<Success>>
    {
        private static readonly ReadOnlyDictionary<OrderStatus, OrderStatus[]> AllowedTransitions =
            new Dictionary<OrderStatus, OrderStatus[]>
            {
                [OrderStatus.Pending] = [OrderStatus.InPreparation, OrderStatus.Cancelled],
                [OrderStatus.InPreparation] = [OrderStatus.Completed, OrderStatus.Cancelled],
                [OrderStatus.Completed] = [],
                [OrderStatus.Cancelled] = []
            }.AsReadOnly();
        
        public async Task<ErrorOr<Success>> Handle(Command command, CancellationToken ct)
        {
            var order = await db.Orders
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);

            if (order is null)
                return Error.NotFound(
                    "Order.NotFound",
                    $"Order with ID {command.OrderId} was not found.");

            var isAllowed = AllowedTransitions.TryGetValue(order.Status, out var allowed)
                            && allowed.Contains(command.NewStatus);
            
            if (!isAllowed)
                return Error.Conflict(
                    "Order.InvalidStatus",
                    $"Cannot change order status from {order.Status} to {command.NewStatus}.");

            order.Status = command.NewStatus;

            await db.SaveChangesAsync(ct);

            return Result.Success;
        }
    }
}

public static class UpdateOrderStatusEndpoint
{
    public static IEndpointRouteBuilder MapUpdateOrderStatus(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/{id:guid}/status",
                async (Guid id, UpdateOrderStatus.Request request, ISender sender, CancellationToken ct) =>
                {
                    var command = new UpdateOrderStatus.Command(id, request.NewStatus);

                    var result = await sender.Send(command, ct);

                    return result.ToNoContent();
                })
            .WithName("UpdateOrderStatus")
            .WithSummary("Updates order status")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Chef),
                nameof(UserRole.Manager)
            ))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        return app;
    }
}