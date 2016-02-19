using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class PackageSearchMetadataV2FeedTests
    {
        [Fact]
        public void PackageSearchMetadata_Basic()
        {
            // Arrange
            var testPackage = CreateTestPackageInfo(new List<string>() { "James Newkirk", "Brad Wilson" },
                                                    null,
                                                    "https://raw.githubusercontent.com/xunit/media/master/logo-512-transparent.png",
                                                    "https://github.com/xunit/xunit",
                                                    "",
                                                    "invalidUri"
                                                    );

            // Act
            var metaData = new PackageSearchMetadataV2Feed(testPackage);

            // Assert
            Assert.True(metaData.Authors.Equals("James Newkirk, Brad Wilson"));
            Assert.True(metaData.Owners.Equals(""));
            Assert.Equal("https://raw.githubusercontent.com/xunit/media/master/logo-512-transparent.png", metaData.IconUrl.AbsoluteUri);
            Assert.Equal("https://github.com/xunit/xunit", metaData.LicenseUrl.AbsoluteUri);
            Assert.Null(metaData.ProjectUrl);
            Assert.Null(metaData.ReportAbuseUrl);
        }


        private V2FeedPackageInfo CreateTestPackageInfo(IEnumerable<string> authors, IEnumerable<string> owners,
            string iconUrl, string licenseUrl, string projectUrl, string reportAbuseUrl)
        {
            return new V2FeedPackageInfo(new PackageIdentity("test", NuGetVersion.Parse("1.0.0")),
                                         "title", "summary", "description", authors, owners,
                                         iconUrl, licenseUrl, projectUrl, reportAbuseUrl, "tags", null, "dependencies",
                                         false, "downloadUrl", "0", "packageHash", "packageHashAlgorithm", new NuGetVersion("3.0"));
        }
    }
}
