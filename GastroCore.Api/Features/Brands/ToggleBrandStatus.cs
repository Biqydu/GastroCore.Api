using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using GastroCore.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Brands;

public sealed record ToggleBrandStatusCommand(Guid Id) : IRequest<ErrorOr<Success>>;

public sealed class ToggleBrandStatusHandler(AppDbContext db)
    : IRequestHandler<ToggleBrandStatusCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(ToggleBrandStatusCommand command, CancellationToken ct)
    {
        var brand = await db.Brands.FirstOrDefaultAsync(b => b.Id == command.Id, ct);

        if (brand is null)
            return Error.NotFound(
                "Brand.NotFound",
                $"Brand with ID '{command.Id}' was not found.");

        brand.IsActive = !brand.IsActive;

        await db.SaveChangesAsync(ct);

        return Result.Success;
    }
}

public static class ToggleBrandStatusEndpoint
{
    public static IEndpointRouteBuilder MapToggleBrandStatus(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/{id:guid}/toggle", async (
                Guid id,
                ISender mediator,
                CancellationToken ct) =>
            {
                var command = new ToggleBrandStatusCommand(id);

                var result = await mediator.Send(command, ct);

                return result.ToNoContent();
            })
            .WithName("ToggleBrandStatus")
            .WithSummary("Toggles the active status of a brand.")
            .RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Manager)))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}