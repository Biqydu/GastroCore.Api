using ErrorOr;
using ErrorOrAspNetCoreExtensions;
using FluentValidation;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GastroCore.Api.Features.Brands;

public sealed record CreateBrandRequest(string Name);

public sealed record CreateBrandCommand(string Name) : IRequest<ErrorOr<CreateBrandResponse>>;

public sealed record CreateBrandResponse(Guid Id, string Name, bool IsActive);

public sealed class CreateBrandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
    }
}

public sealed class CreateBrandHandler(AppDbContext db, ICurrentUserContext userContext)
    : IRequestHandler<CreateBrandCommand, ErrorOr<CreateBrandResponse>>
{
    public async Task<ErrorOr<CreateBrandResponse>> Handle(CreateBrandCommand command, CancellationToken ct)
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

        return new CreateBrandResponse(brand.Id, brand.Name, brand.IsActive);
    }
}

public static class CreateBrandEndpoint
{
    public static IEndpointRouteBuilder MapCreateBrand(this IEndpointRouteBuilder app)
    {
        app.MapPost("/",
                async (CreateBrandRequest request, ISender sender,
                    CancellationToken ct) =>
                {
                    var command = new CreateBrandCommand(request.Name);

                    var result = await sender.Send(command, ct);

                    return result.ToCreated(response => $"/api/brands/{response.Id}"
                    );
                })
            .WithName("CreateBrand")
            .WithSummary("Creates a new brand.")
            .RequireAuthorization(policy => policy.RequireRole(
                nameof(UserRole.Manager)
            ))
            .Produces<CreateBrandResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}