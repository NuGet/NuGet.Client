using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class ConfigCommandTest
    {
        [Fact]
        public void ConfigCommand_ChangeDefaultConfigFile()
        {
            using (var preserver = new DefaultConfigurationFilePreserver())
            {
                // Arrange
                string[] args = new string[] {
                    "config",
                    "-Set",
                    "Name1=Value1",
                    "-Set",
                    "HTTP_PROXY=http://127.0.0.1",
                    "-Set",
                    @"HTTP_PROXY.USER=domain\user"
                };

                // Act
                int result = Program.Main(args);

                // Assert
                Assert.Equal(0, result);

                var settings = Settings.LoadDefaultSettings(
                    new PhysicalFileSystem(@"c:\"), null, null);
                var values = settings.GetValues("config", isPath: false);
                AssertEqualCollections(values, new[] { "Name1", "Value1", "HTTP_PROXY", "http://127.0.0.1", "HTTP_PROXY.USER", @"domain\user" });

            }
        }

        [Fact]
        public void ConfigCommand_ChangeUserDefinedConfigFile()
        {
            var configFile = Path.GetTempFileName();
            Util.CreateFile(Path.GetDirectoryName(configFile), Path.GetFileName(configFile), "<configuration/>");
            try
            {
                string[] args = new string[] {
                    "config",
                    "-Set",
                    "Name1=Value1",
                    "-Set",
                    "HTTP_PROXY=http://127.0.0.1",
                    "-Set",
                    @"HTTP_PROXY.USER=domain\user",
                    "-ConfigFile",
                    configFile
                };

                int result = Program.Main(args);

                // Assert
                Assert.Equal(0, result);

                var settings = Settings.LoadDefaultSettings(
                    new PhysicalFileSystem(Path.GetDirectoryName(configFile)),
                    Path.GetFileName(configFile),
                    null);
                var values = settings.GetValues("config", isPath: false);
                AssertEqualCollections(values, new[] { "Name1", "Value1", "HTTP_PROXY", "http://127.0.0.1", "HTTP_PROXY.USER", @"domain\user" });

            }
            finally
            {
                // cleanup
                File.Delete(configFile);
            }
        }

        [Fact]
        public void ConfigCommand_GetValueWithAsPathOption()
        {
            // Arrange
            var configFile = Path.GetTempFileName();
            Util.CreateFile(Path.GetDirectoryName(configFile), Path.GetFileName(configFile), "<configuration/>");

            try
            {
                string[] args = new string[] {
                    "config",
                    "-Set",
                    "Name1=Value1",
                    "-ConfigFile",
                    configFile
                };
                Program.Main(args);

                // Act
                args = new string[] {
                    "config",
                    "Name1",
                    "-AsPath",
                    "-ConfigFile",
                    configFile
                };
                MemoryStream memoryStream = new MemoryStream();
                TextWriter writer = new StreamWriter(memoryStream);
                Console.SetOut(writer);
                int r = Program.Main(args);
                writer.Close();
                var output = Encoding.Default.GetString(memoryStream.ToArray());

                // Assert
                Assert.Equal(0, r);

                var expectedValue = Path.Combine(Path.GetDirectoryName(configFile), "Value1")
                    + Environment.NewLine;
                Assert.Equal(expectedValue, output);
            }
            finally
            {
                // cleanup
                File.Delete(configFile);
            }
        }

        private void AssertEqualCollections(IList<SettingValue> actual, string[] expected)
        {
            Assert.Equal(actual.Count, expected.Length / 2);
            for (int i = 0; i < actual.Count; ++i)
            {
                Assert.Equal(expected[2 * i], actual[i].Key);
                Assert.Equal(expected[2 * i + 1], actual[i].Value);
            }
        }
    }
}
