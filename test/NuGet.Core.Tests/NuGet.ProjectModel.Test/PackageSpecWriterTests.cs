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
  ""authors"": [ ""todd ""],
  ""description"": ""test"",
  ""dependencies"": {
    ""packageA"": {
      ""target"": ""project""
      }
    },
    ""frameworks"": {
      ""net46"": {
    }
  }
}";

            // Act
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
                        textWriter.Flush();
                        memoryStream.Flush();

                        Console.WriteLine("TODD - memoryStream.Length: " + memoryStream.Length);
                        byte[] buffer = new byte[memoryStream.Length + 1];
                        memoryStream.Read(buffer, 0, (int)memoryStream.Length);
                        Encoding encoding = new System.Text.UnicodeEncoding();
                        text = encoding.GetString(buffer);
                    }
                }
            }

            // Assert
            Assert.Equal(json, text);
        }
    }
}
