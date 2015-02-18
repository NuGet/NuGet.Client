using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Client;
using NuGet.Versioning;
using Xunit;

namespace Client.V3Test
{
    public class ReportAbuseTests
    {
        [Fact]
        public void ConstructorThrowsArgumentNullExceptionForNull()
        {
            Assert.Throws<ArgumentNullException>(() => new V3ReportAbuseResource(null));
        }

        [Fact]
        public void ConstructorThrowsArgumentNullExceptionForEmpty()
        {
            Assert.Throws<ArgumentNullException>(() => new V3ReportAbuseResource(Enumerable.Empty<Uri>()));
        }

        [Fact]
        public void GetFirstUncheckedUriAppliesTemplate()
        {
            // Arrange
            var templates = new[] {
                new Uri("http://report.abuse/{id}/{version}/{id-lower}/{version-lower}", UriKind.Absolute),
                new Uri("http://report.abuse/will-not-be-evaluated", UriKind.Absolute)
            };

            var resource = new V3ReportAbuseResource(templates);

            // Act
            Uri actual = resource.GetReportAbuseUrl("PackageID", new NuGetVersion("1.2.3-RC"));

            // Assert
            Assert.Equal(new Uri("http://report.abuse/PackageID/1.2.3-RC/packageid/1.2.3-rc", UriKind.Absolute), actual);
        }

        [Fact]
        public async void UnavailableUrlsAreNotReturned()
        {
            // Arrange
            var templates = new[] {
                new Uri("http://doesnotexist.nuget.org/{id-lower}/{version-lower}", UriKind.Absolute),
                new Uri("http://doesnotexist.nuget.org/{id}/{version}", UriKind.Absolute)
            };

            var resource = new V3ReportAbuseResource(templates);

            // Act
            Uri actual = await resource.GetReportAbuseUrl("PackageID", new NuGetVersion("1.2.3-RC"), new System.Threading.CancellationToken());

            // Assert
            Assert.Null(actual);
        }

        [Fact]
        public async void FirstAvailableUrlIsReturned()
        {
            // Arrange
            var templates = new[] {
                new Uri("http://doesnotexist.nuget.org/{id-lower}/{version-lower}", UriKind.Absolute),
                new Uri("http://api.nuget.org/v3/index.json", UriKind.Absolute) // All we need is a document that we know exists
            };

            var resource = new V3ReportAbuseResource(templates);

            // Act
            Uri actual = await resource.GetReportAbuseUrl("PackageID", new NuGetVersion("1.2.3-RC"), new System.Threading.CancellationToken());

            // Assert
            Assert.Equal(templates[1], actual);
        }
    }
}
