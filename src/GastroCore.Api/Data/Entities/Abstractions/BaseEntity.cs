namespace GastroCore.Api.Data.Entities.Abstractions;

public abstract class BaseEntity
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}