using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class JsonPackageSpecWriterTests
    {
        [Fact]
        public void PackageSpecWrite_ReadWriteDependencies()
        {
            // Arrange
            var json = @"{
  ""title"": ""My Title"",
  ""version"": ""1.2.3"",
  ""description"": ""test"",
  ""authors"": [
    ""author1"",
    ""author2""
  ],
  ""copyright"": ""2016"",
  ""language"": ""en-US"",
  ""packInclude"": {
    ""file"": ""file.txt""
  },
  ""packOptions"": {
    ""owners"": [
      ""owner1"",
      ""owner2""
    ],
    ""tags"": [
      ""tag1"",
      ""tag2""
    ],
    ""projectUrl"": ""http://my.url.com"",
    ""iconUrl"": ""http://my.url.com"",
    ""summary"": ""Sum"",
    ""releaseNotes"": ""release noted"",
    ""licenseUrl"": ""http://my.url.com"",
    ""requireLicenseAcceptance"": ""False""
  },
  ""scripts"": {
    ""script1"": [
      ""script.js""
    ]
  },
  ""dependencies"": {
    ""packageA"": {
      ""suppressParent"": ""All"",
      ""target"": ""Project""
    }
  },
  ""frameworks"": {
    ""net46"": {}
  }
}";
            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void PackageSpecWrite_ReadWriteSinglePackageType()
        {
            // Arrange
            var json = @"{
  ""packOptions"": {
    ""requireLicenseAcceptance"": ""False"",
    ""packageType"": ""DotNetTool""
  }
}";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        [Fact]
        public void PackageSpecWrite_ReadWriteMultiplePackageType()
        {
            // Arrange
            var json = @"{
  ""packOptions"": {
    ""requireLicenseAcceptance"": ""False"",
    ""packageType"": [
      ""DotNetTool"",
      ""Dependency""
    ]
  }
}";

            // Act & Assert
            VerifyJsonPackageSpecRoundTrip(json);
        }

        private static void VerifyJsonPackageSpecRoundTrip(string json)
        {
            // Arrange & Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "testName", @"C:\fake\path");

            JObject jsonObject = new JObject();
            JsonPackageSpecWriter.WritePackageSpec(spec, jsonObject);

            string text;
            using (var memoryStream = new MemoryStream())
            {
                using (var textWriter = new StreamWriter(memoryStream))
                {
                    using (var jsonWriter = new JsonTextWriter(textWriter))
                    {
                        jsonWriter.Formatting = Formatting.Indented;
                        jsonObject.WriteTo(jsonWriter);

                        jsonWriter.Flush();
                    }
                }

                text = Encoding.UTF8.GetString(memoryStream.ToArray());
            }

            // Assert
            Assert.Equal(json, text);
        }
    }
}
