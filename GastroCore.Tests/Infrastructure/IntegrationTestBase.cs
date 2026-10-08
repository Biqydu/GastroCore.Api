using GastroCore.Api.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace GastroCore.Tests.Infrastructure;

[Collection(DatabaseCollection.Name)]
public abstract class IntegrationTestBase(TestWebAppFactory factory) : IAsyncLifetime
{
    protected TestWebAppFactory Factory { get; } = factory;

    public Task InitializeAsync()
    {
        Factory.CurrentUser.Reset();
        return Factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    protected async Task<TResponse> Send<TResponse>(IRequest<TResponse> request)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(request);
    }

    protected async Task ExecuteDb(Func<AppDbContext, Task> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
    }

    protected async Task<T> QueryDb<T>(Func<AppDbContext, Task<T>> query)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db);
    }
}