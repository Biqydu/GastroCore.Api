using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using FluentValidation;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Features.Ingredients;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Brands;

public static class UpdateBrand
{
    public sealed record Request(string Name);

    public sealed record Command(Guid Id, string Name) : IRequest<ErrorOr<Response>>;

    public sealed record Response(Guid Id, string Name);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
        }
    }

    public sealed class Handler(AppDbContext db)
        : IRequestHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(Command command, CancellationToken ct)
        {
            var brand = await db.Brands
                .FirstOrDefaultAsync(b => b.Id == command.Id, ct);

            if (brand is null)
                return Error.NotFound(
                    "Brand.NotFound",
                    $"Brand with ID '{command.Id}' was not found.");

            brand.Name = command.Name;

            return new Response(brand.Id, brand.Name);
        }
    }
}

public static class UpdateBrandEndpoint
{
    public static IEndpointRouteBuilder MapUpdateBrand(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{id:guid}", async (Guid id, UpdateBrand.Request request, ISender sender, CancellationToken ct) =>
            {
                var command = new UpdateBrand.Command(id, request.Name);

                var result = await sender.Send(command, ct);

                return result.ToOk();
            })
            .WithName("UpdateBrand")
            .WithSummary("Updates brand")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Manager)
            ))
            .Produces<UpdateIngredientResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}