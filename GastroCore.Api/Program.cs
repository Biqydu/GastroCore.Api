using FluentValidation;
using GastroCore.Api.Behaviors;
using GastroCore.Api.Common;
using GastroCore.Api.Data;
using GastroCore.Api.Data.Entities;
using GastroCore.Api.Features.Brands;
using GastroCore.Api.Features.Ingredients;
using GastroCore.Api.Features.Recipes;
using GastroCore.Api.Features.Recipes.Common;
using GastroCore.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("PostgresConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<AppUser>()
    .AddRoles<AppRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(opts => { opts.Theme = ScalarTheme.DeepSpace; });
}

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    foreach (var roleName in Enum.GetNames<UserRole>())
        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new AppRole(roleName));

    const string integrationEmail = "integration-bot@gastrocore.local";

    var integrationUser = await userManager.FindByEmailAsync(integrationEmail);

    if (integrationUser == null)
    {
        var newBot = new AppUser
        {
            UserName = "UberEatsGlovoIntegrationBot",
            Email = integrationEmail,
            EmailConfirmed = true
        };

        var securePassword = Guid.NewGuid() + "A1!";

        var result = await userManager.CreateAsync(newBot, securePassword);

        if (result.Succeeded) await userManager.AddToRoleAsync(newBot, nameof(UserRole.Integration));
    }
}

var api = app.MapGroup("/api")
    .MapIngredientsModule()
    .MapBrandsModule()
    .MapRecipesModule();

api.MapGroup("/auth")
    .WithTags("Authentication")
    .MapIdentityApi<AppUser>();

app.Run();