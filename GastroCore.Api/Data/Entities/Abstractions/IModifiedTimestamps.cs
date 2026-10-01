namespace GastroCore.Api.Data.Entities.Abstractions;

public interface IModifiedTimestamps : ICreatedTimestamps
{
    public DateTimeOffset? UpdatedAt { get; set; }
}