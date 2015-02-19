using NuGet.Data;
using NuGet.Versioning;
using System;
using Xunit;
using Xunit.Extensions;

namespace DataTest
{
    public class UriTests
    {
        /// <summary>
        /// Test out our package uri template processing with all of the tokens we support.
        /// </summary>
        /// <remarks>
        /// Be sure to check for case preservation along with the lower-case version of id/version.
        /// </remarks>
        [Theory]
        [InlineData("http://foo.com/", "packageId", "1.0.0-Beta", "http://foo.com/")]
        [InlineData("http://foo.com/{id}", "packageId", "1.0.0-Beta", "http://foo.com/packageId")]
        [InlineData("http://foo.com/{id-lower}", "packageId", "1.0.0-Beta", "http://foo.com/packageid")]
        [InlineData("http://foo.com/{version}", "packageId", "1.0.0-Beta", "http://foo.com/1.0.0-Beta")]
        [InlineData("http://foo.com/{version-lower}", "packageId", "1.0.0-Beta", "http://foo.com/1.0.0-beta")]
        [InlineData("http://foo.com/{id}/{version}", "packageId", "1.0.0-Beta", "http://foo.com/packageId/1.0.0-Beta")]
        [InlineData("http://foo.com/{id-lower}/{version-lower}", "packageId", "1.0.0-Beta", "http://foo.com/packageid/1.0.0-beta")]
        [InlineData("http://foo.com/query?id={id}&version={version}", "packageId", "1.0.0-Beta", "http://foo.com/query?id=packageId&version=1.0.0-Beta")]
        public void ApplyPackageIdVersionToUriTemplate(string template, string id, string version, string expected)
        {
            var nugetVersion = new NuGetVersion(version);
            var actual = Utility.ApplyPackageIdVersionToUriTemplate(new Uri(template), id, nugetVersion);
            Assert.Equal(expected, actual.ToString());
        }

        /// <summary>
        /// Test out our package uri template processing with all of the tokens we support.
        /// </summary>
        /// <remarks>
        /// Be sure to check for case preservation along with the lower-case version of id/version.
        /// </remarks>
        [Theory]
        [InlineData("http://foo.com/", "packageId", "http://foo.com/")]
        [InlineData("http://foo.com/{id}", "packageId", "http://foo.com/packageId")]
        [InlineData("http://foo.com/{id-lower}", "packageId", "http://foo.com/packageid")]
        [InlineData("http://foo.com/query?id={id}", "packageId", "http://foo.com/query?id=packageId")]
        public void ApplyPackageIdToUriTemplate(string template, string id, string expected)
        {
            var actual = Utility.ApplyPackageIdToUriTemplate(new Uri(template), id);
            Assert.Equal(expected, actual.ToString());
        }

        [Fact]
        public void ApplyPackageIdToUriTemplates()
        {
            var templates = new Uri[] { new Uri("http://foo.com/{id}", UriKind.Absolute), new Uri("http://bar.com/{id-lower}", UriKind.Absolute) };
            var packageId = "packageId";

            var expected = new Uri[] { new Uri("http://foo.com/packageId", UriKind.Absolute), new Uri("http://bar.com/packageid", UriKind.Absolute) };
            var actual = Utility.ApplyPackageIdToUriTemplate(templates, packageId);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ApplyPackageIdVersionToUriTemplates()
        {
            var templates = new Uri[] { new Uri("http://foo.com/{id}/{version}", UriKind.Absolute), new Uri("http://bar.com/{id-lower}/{version-lower}", UriKind.Absolute) };
            var packageId = "packageId";
            var packageVersion = new NuGetVersion("1.0.0-Beta");

            var expected = new Uri[] { new Uri("http://foo.com/packageId/1.0.0-Beta", UriKind.Absolute), new Uri("http://bar.com/packageid/1.0.0-beta", UriKind.Absolute) };
            var actual = Utility.ApplyPackageIdVersionToUriTemplate(templates, packageId, packageVersion);

            Assert.Equal(expected, actual);
        }
    }
}
