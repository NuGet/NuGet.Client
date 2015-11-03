using System;
using System.Collections.Generic;
using System.IO;
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
                // Set the working directory to C:\, otherwise,
                // the test will change the nuget.config at the code repo's root directory
                int result = Program.MainCore(@"c:\", args);

                // Assert
                Assert.Equal(0, result);

                var settings = Configuration.Settings.LoadDefaultSettings(
                    @"c:\", null, null);
                var values = settings.GetSettingValues("config", isPath: false);
                AssertEqualCollections(values, new[]
                    {
                        "Name1",
                        "Value1",
                        "HTTP_PROXY",
                        "http://127.0.0.1",
                        "HTTP_PROXY.USER",
                        @"domain\user"
                    });
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

                var settings = Configuration.Settings.LoadDefaultSettings(
                    Path.GetDirectoryName(configFile),
                    Path.GetFileName(configFile),
                    null);
                var values = settings.GetSettingValues("config", isPath: false);
                AssertEqualCollections(values, new[]
                    {
                        "Name1",
                        "Value1",
                        "HTTP_PROXY",
                        "http://127.0.0.1",
                        "HTTP_PROXY.USER",
                        @"domain\user"
                    });
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
            var nugetexe = Util.GetNuGetExePath();
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
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    waitForExit: true);

                // Act
                args = new string[] {
                    "config",
                    "Name1",
                    "-AsPath",
                    "-ConfigFile",
                    configFile
                };

                result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    waitForExit: true);

                var output = result.Item2;

                // Assert
                Assert.Equal(0, result.Item1);

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

        private void AssertEqualCollections(IList<Configuration.SettingValue> actual, string[] expected)
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
