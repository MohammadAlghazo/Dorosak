namespace Dorosak.Application.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class InfrastructureTestGroup : ICollectionFixture<InfrastructureFixture>
{
    public const string Name = "Infrastructure";
}
