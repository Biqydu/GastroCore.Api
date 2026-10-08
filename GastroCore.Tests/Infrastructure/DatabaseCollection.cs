namespace GastroCore.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<TestWebAppFactory>
{
    public const string Name = "Database";
}