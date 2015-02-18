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
        public async Task<Uri> UnavailableUrlsAreNotReturned()
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

            // Return a value to work around http://xunit.codeplex.com/workitem/9800
            return actual;
        }

        [Fact]
        public async Task<Uri> FirstAvailableUrlIsReturned()
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

            // Return a value to work around http://xunit.codeplex.com/workitem/9800
            return actual;
        }

        [Fact]
        public async Task<Uri> DifferentHttpFailuresAreHandled()
        {
            // Arrange
            var templates = new[] {
                new Uri("http://dnsfailure.nuget.org/{id-lower}/{version-lower}", UriKind.Absolute),
                new Uri("http://api.nuget.org/404-not-found-failure/{id}/{version}", UriKind.Absolute),
                new Uri("http://api.nuget.org/This/Path/Returns/400/{id}/{version}", UriKind.Absolute),
                new Uri("http://api.nuget.org/v3/index.json", UriKind.Absolute) // All we need is a document that we know exists
            };

            var resource = new V3ReportAbuseResource(templates);

            // Act
            Uri actual = await resource.GetReportAbuseUrl("PackageID", new NuGetVersion("1.2.3-RC"), new System.Threading.CancellationToken());

            // Assert
            Assert.Equal(templates[3], actual);

            // Return a value to work around http://xunit.codeplex.com/workitem/9800
            return actual;
        }

        [Fact]
        public async Task<Uri> TemporaryRedirectsAreHandled()
        {
            // Arrange
            var templates = new[] {
                new Uri("http://api.nuget.org", UriKind.Absolute) // This redirects into the v3/index.json with a temporary redirect
            };

            var resource = new V3ReportAbuseResource(templates);

            // Act
            Uri actual = await resource.GetReportAbuseUrl("PackageID", new NuGetVersion("1.2.3-RC"), new System.Threading.CancellationToken());

            // Assert
            Assert.Equal(templates[0], actual);

            // Return a value to work around http://xunit.codeplex.com/workitem/9800
            return actual;
        }

        [Fact]
        public async Task<Uri> PermanentRedirectsAreHandled()
        {
            // Arrange
            var templates = new[] {
                new Uri("http://api.nuget.org/v3", UriKind.Absolute) // This redirects into the v3/index.json with a permanent redirect
            };

            var resource = new V3ReportAbuseResource(templates);

            // Act
            Uri actual = await resource.GetReportAbuseUrl("PackageID", new NuGetVersion("1.2.3-RC"), new System.Threading.CancellationToken());

            // Assert
            Assert.Equal(templates[0], actual);

            // Return a value to work around http://xunit.codeplex.com/workitem/9800
            return actual;
        }
    }
}
