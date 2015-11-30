using NuGet.Protocol.Core.v3;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class ReportAbuseResourceV3Tests
    {
        [Theory]
        [InlineData("")]
        [InlineData("https://test.nuget.org/ReportAbuse/{id}")]
        [InlineData("https://test.nuget.org/ReportAbuse/{version}")]
        [InlineData("https://test.nuget.org/ReportAbuse/{0}/{1}")]
        [InlineData(null)]
        public void GetReportAbuseUrlFallsBackToDefaultForInvalidUriTemplate(string uriTemplate)
        {
            const string expectedDefault = "https://www.nuget.org/packages/TestPackage/1.0.0/ReportAbuse";
            var resource = new ReportAbuseResourceV3(uriTemplate);

            var actual = resource.GetReportAbuseUrl("TestPackage", NuGetVersion.Parse("1.0.0"));

            Assert.Equal(expectedDefault, actual.ToString());
        }
    }
}