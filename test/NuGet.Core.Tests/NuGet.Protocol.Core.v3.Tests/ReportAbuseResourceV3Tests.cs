using NuGet.Protocol;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class ReportAbuseResourceV3Tests
    {
        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void GetReportAbuseUrlFallsBackToDefaultForInvalidUriTemplate(string uriTemplate)
        {
            const string expectedResult = "https://www.nuget.org/packages/TestPackage/1.0.0/ReportAbuse";
            var resource = new ReportAbuseResourceV3(uriTemplate);

            var actual = resource.GetReportAbuseUrl("TestPackage", NuGetVersion.Parse("1.0.0"));

            Assert.Equal(expectedResult, actual.ToString());
        }

        [Fact]
        public void GetReportAbuseUrlReplacesIdAndVersionTokensInUriTemplateWhenAvailable()
        {
            const string uriTemplate = "https://test.nuget.org/ReportAbuse/{id}/{version}";
            const string expectedResult = "https://test.nuget.org/ReportAbuse/TestPackage/1.0.0";
            var resource = new ReportAbuseResourceV3(uriTemplate);

            var actual = resource.GetReportAbuseUrl("TestPackage", NuGetVersion.Parse("1.0.0"));

            Assert.Equal(expectedResult, actual.ToString());
        }
    }
}