using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using FluentValidation;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Brands;

public static class CreateBrand
{
    public sealed record Request(string Name);

    public sealed record Command(string Name) : IRequest<ErrorOr<Response>>;

    public sealed record Response(Guid Id, string Name, bool IsActive);

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
        }
    }

    public sealed class Handler(AppDbContext db, ICurrentUserContext userContext)
        : IRequestHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(Command command, CancellationToken ct)
        {
            var brandExists = await db.Brands
                .AnyAsync(b => EF.Functions.ILike(b.Name, command.Name.Trim()), ct);

            if (brandExists)
                return Error.Conflict(
                    "Brand.AlreadyExists",
                    $"Brand with name '{command.Name}' already exists.");

            var brand = new Brand
            {
                Name = command.Name,
                CreatedBy = userContext.Id
            };

            db.Brands.Add(brand);

            await db.SaveChangesAsync(ct);

            return new Response(brand.Id, brand.Name, brand.IsActive);
        }
    }
}

public static class CreateBrandEndpoint
{
    public static IEndpointRouteBuilder MapCreateBrand(this IEndpointRouteBuilder app)
    {
        app.MapPost("/",
                async (CreateBrand.Request request, ISender sender,
                    CancellationToken ct) =>
                {
                    var command = new CreateBrand.Command(request.Name);

                    var result = await sender.Send(command, ct);

                    return result.ToCreated(response => $"/api/brands/{response.Id}"
                    );
                })
            .WithName("CreateBrand")
            .WithSummary("Creates a new brand.")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Manager)
            ))
            .Produces<CreateBrand.Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}