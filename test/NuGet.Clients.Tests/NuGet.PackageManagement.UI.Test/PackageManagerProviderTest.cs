using System;
using System.Collections.Generic;
using Xunit;
using NuGet.VisualStudio;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition.Hosting;
using System.Reflection;
using NuGet.PackageManagement.UI;

namespace NuGet.CommandLine.Test
{
    public class PackageManagerProviderTest
    {
        [ImportMany(typeof(IVsPackageManagerProvider))]
        public IEnumerable<Lazy<IVsPackageManagerProvider, IOrderable>> PackageManagerProviders { get; set; }

        public PackageManagerProviderTest()
        {
            Init();
        }

        [Fact]
        public void PackageManagerProvider_SimpleSort()
        {
            // Act
            var sorted = PackageManagerProviderUtility.Sort(PackageManagerProviders, 3);

            // Assert
            Assert.Equal("test-version0", sorted[0].PackageManagerId);
            Assert.Equal("test-version1", sorted[1].PackageManagerId);
            Assert.Equal("test-version2", sorted[2].PackageManagerId);
        }

        [Fact]
        public void PackageManagerProvider_DuplicatedManagerId()
        {
            // Act
            var sorted = PackageManagerProviderUtility.Sort(PackageManagerProviders, 3);

            // Assert
            Assert.Equal("testUpdate", sorted[0].PackageManagerName);
            Assert.Equal("test-version1", sorted[1].PackageManagerId);
            Assert.Equal("test-version2", sorted[2].PackageManagerId);
        }

        [Fact]
        public void PackageManagerProvider_LimitedManagerProviders()
        {
            // Act
            var sorted = PackageManagerProviderUtility.Sort(PackageManagerProviders, 1);

            // Assert
            Assert.Equal(1, sorted.Count);
        }

        private void Init()
        {
            var catalog = new AssemblyCatalog
               (Assembly.GetExecutingAssembly());

            var container = new CompositionContainer(catalog);
            container.ComposeParts(this);
        }
    }
}
