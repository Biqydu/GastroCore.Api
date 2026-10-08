namespace GastroCore.Api.Data.Entities.Abstractions;

public interface ICreatedTimestamps
{
    public DateTimeOffset CreatedAt { get; set; }
}