using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using FluentValidation;
using GastroCore.Api.Data;
using GastroCore.Api.Features.Ingredients;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Brands;

public sealed record UpdateBrandRequest(string Name);

public sealed record UpdateBrandCommand(Guid Id, string Name) : IRequest<ErrorOr<UpdateBrandResponse>>;

public sealed record UpdateBrandResponse(Guid Id, string Name);

public sealed class UpdateBrandValidator : AbstractValidator<UpdateBrandCommand>
{
    public UpdateBrandValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
    }
}

public sealed class UpdateBrandHandler(AppDbContext db)
    : IRequestHandler<UpdateBrandCommand, ErrorOr<UpdateBrandResponse>>
{
    public async Task<ErrorOr<UpdateBrandResponse>> Handle(UpdateBrandCommand command, CancellationToken ct)
    {
        var brand = await db.Brands
            .FirstOrDefaultAsync(b => b.Id == command.Id, ct);

        if (brand is null)
            return Error.NotFound(
                "Brand.NotFound",
                $"Brand with ID '{command.Id}' was not found.");

        brand.Name = command.Name;

        return new UpdateBrandResponse(brand.Id, brand.Name);
    }
}

public static class UpdateBrandEndpoint
{
    public static IEndpointRouteBuilder MapUpdateBrand(this IEndpointRouteBuilder app)
    {
        app.MapPut("/{id:guid}", async (Guid id, UpdateBrandRequest request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateBrandCommand(id, request.Name);

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
        .ProducesValidationProblem();;

        return app;
    }
}