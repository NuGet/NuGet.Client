using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class CachingSourceProviderTests
    {
        private class CustomResource : INuGetResource
        {
            public int Value { get; set; }
        }

        private class CustomResourceProvider : ResourceProvider
        {
            public const int DefaultValue = 5;

            public CustomResourceProvider() : base(typeof(CustomResource))
            {
            }

            public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source,
                CancellationToken token)
            {
                var resource = new CustomResource {Value = DefaultValue};
                return Task.FromResult(new Tuple<bool, INuGetResource>(true, resource));
            }
        }

        [Fact]
        public void Ctor_CustomResourceProviders()
        {
            var packageSourceProvider = new PackageSourceProvider(NullSettings.Instance);
            var resourceProviders = Repository.Provider.GetCoreV3().Concat(
                new[] {new Lazy<INuGetResourceProvider>(() => new CustomResourceProvider())});
            var cachingSourceProvider = new CachingSourceProvider(packageSourceProvider, resourceProviders);
            var sourceRepository = cachingSourceProvider.CreateRepository(new PackageSource("https://unit.test"));
            var customResource = sourceRepository.GetResource<CustomResource>();
            Assert.Equal(CustomResourceProvider.DefaultValue, customResource.Value);
        }

        [Fact]
        public void Ctor_NullArguments()
        {
            var packageSourceProvider = new PackageSourceProvider(NullSettings.Instance);
            var resourceProviders = Repository.Provider.GetCoreV3();

            Assert.Throws<ArgumentNullException>(() =>  new CachingSourceProvider(null));
            Assert.Throws<ArgumentNullException>(() =>  new CachingSourceProvider(null, null));
            Assert.Throws<ArgumentNullException>(() =>  new CachingSourceProvider(packageSourceProvider, null));
            Assert.Throws<ArgumentNullException>(() =>  new CachingSourceProvider(null, resourceProviders));
        }
    }
}
