using System.Threading;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class HttpSourceResourceProviderTests
    {
        private const string FakeSource = "https://fake.server/users.json";

        [Fact]
        public void TryCreate_WithDifferentSourceRepositoryCredentials_ReturnsDifferentHttpSourceResources()
        {
            var provider = new HttpSourceResourceProvider();
            var a = provider.TryCreate(CreateSourceRepository("Foo"), CancellationToken.None);
            var b = provider.TryCreate(CreateSourceRepository("Bar"), CancellationToken.None);

            Assert.NotSame(a.Result.Item2, b.Result.Item2);
        }

        [Fact]
        public void TryCreate_WithTheSameSourceRepositoryCredentials_ReturnsTheSameHttpSourceResource()
        {
            var provider = new HttpSourceResourceProvider();
            var a = provider.TryCreate(CreateSourceRepository("Foo"), CancellationToken.None);
            var b = provider.TryCreate(CreateSourceRepository("Foo"), CancellationToken.None);

            Assert.Same(a.Result.Item2, b.Result.Item2);
        }

        private static SourceRepository CreateSourceRepository(string password)
        {
            return new SourceRepository(
                new PackageSource(FakeSource) { Credentials = new PackageSourceCredential(FakeSource, "user", password, true)},
                new[]
                {
                    new HttpSourceResourceProvider()
                }
            );
        }
    }
}