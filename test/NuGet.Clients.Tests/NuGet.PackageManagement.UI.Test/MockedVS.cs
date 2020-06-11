using Xunit;
using Microsoft.VisualStudio.Sdk.TestFramework;

namespace NuGet.PackageManagement.UI.Test
{
    /// <summary>
    /// Defines the "MockedVS" xunit test collection.
    /// </summary>
    [CollectionDefinition(Collection)]
    public class MockedVS : ICollectionFixture<GlobalServiceProvider>, ICollectionFixture<MefHostingFixture>
    {
        /// <summary>
        /// The name of the xunit test collection.
        /// </summary>
        public const string Collection = "MockedVS";
    }
}
