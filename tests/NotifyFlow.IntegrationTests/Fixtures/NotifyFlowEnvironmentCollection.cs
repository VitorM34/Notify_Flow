namespace NotifyFlow.IntegrationTests.Fixtures;

[CollectionDefinition(Name)]
public sealed class NotifyFlowEnvironmentCollection : ICollectionFixture<NotifyFlowEnvironmentFixture>
{
    public const string Name = "NotifyFlow Environment";
}
