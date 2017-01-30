using Xunit;

namespace Dotnet.Integration.Test
{
    [CollectionDefinition("Dotnet Integration Tests")]
    public class DotnetIntegrationCollection : ICollectionFixture<MsbuilldIntegrationTestFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
