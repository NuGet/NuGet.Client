using Xunit;

namespace NuGet.Packaging.CrossVerify.Generate.Test
{
    [CollectionDefinition(Name)]
    public class CrossVerifyCollection : ICollectionFixture<GenerateFixture>
    {
        public const string Name = "Cross Verify Test Collection";

        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
