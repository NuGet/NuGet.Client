using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.ProjectModel;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class PackageSpecWriterTests
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
  ""licenseUrl"": ""http://my.url.com"",
  ""copyright"": ""2016"",
  ""language"": ""en-US"",
  ""summary"": ""Sum"",
  ""releaseNotes"": ""release noted"",
  ""requireLicenseAcceptance"": ""False"",
  ""packInclude"": {
    ""file"": ""file.txt""
  },
  ""scripts"": {
    ""script1"": [
      ""script.js""
    ]
  },
  ""dependencies"": {
    ""packageA"": {
      ""include"": ""All"",
      ""suppressParent"": ""Build, ContentFiles, Analyzers"",
      ""type"": ""MainReference,MainSource"",
      ""target"": ""Project""
    }
  },
  ""frameworks"": {
    ""net46"": {}
  }
}";

            // Act
            var spec = JsonPackageSpecReader.GetPackageSpec(json, "testName", @"C:\fake\path");

            JObject jsonObject = new JObject();
            JsonPackageSpecWriter.WritePackageSpec(spec, jsonObject);

            string text;
            byte[] buffer = new byte[json.Length];
            using (var memoryStream = new MemoryStream(buffer, true))
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
            }

            text = System.Text.Encoding.UTF8.GetString(buffer);

            // Assert
            Assert.Equal(json, text);
        }
    }
}
