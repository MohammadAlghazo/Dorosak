namespace Dorosak.Api.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<ApiFixture>
{
    public const string Name = "API";
}
