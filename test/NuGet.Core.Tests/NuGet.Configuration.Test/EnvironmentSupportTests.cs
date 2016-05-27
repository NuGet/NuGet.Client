using NuGet.Test.Utility;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class EnvironmentSupportTests
    {
        private readonly string DefaultNuGetConfigurationWithEnvironmentVariable = @"
<configuration>
    <config>
        <add key='repositoryPath' value='%RP_ENV_VAR%\two' />
    </config>
</configuration>";

        [Fact]
        public void GetValueEvaluatesEnvironmentVariable()
        {
            //Arrange
            var expectedRepositoryPath = @"ONE\two";

            using (var nugetConfigFileFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var nugetConfigFile = "NuGet.config";
                var nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, nugetConfigFile);

                File.WriteAllText(nugetConfigFilePath, DefaultNuGetConfigurationWithEnvironmentVariable);

                Environment.SetEnvironmentVariable("RP_ENV_VAR", "ONE");

                //Act
                ISettings settings = new Settings(nugetConfigFileFolder, nugetConfigFile);

                //Assert
                Assert.Equal(settings.GetValue("config", "repositoryPath", isPath: true), Path.Combine(nugetConfigFileFolder, expectedRepositoryPath));
            }
        }

        [Fact]
        public void GetSettingValuesEvaluatesEnvironmentVariable()
        {
            //Arrange
            var expectedRepositoryPath = @"ONE\two";

            using (var nugetConfigFileFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var nugetConfigFile = "NuGet.config";
                var nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, nugetConfigFile);

                File.WriteAllText(nugetConfigFilePath, DefaultNuGetConfigurationWithEnvironmentVariable);

                Environment.SetEnvironmentVariable("RP_ENV_VAR", "ONE");

                //Act
                ISettings settings = new Settings(nugetConfigFileFolder, nugetConfigFile);

                //Assert
                var settingsForConfig = settings.GetSettingValues("config", isPath: true);
                Assert.Single(settingsForConfig);
                Assert.Equal(settingsForConfig.Single().Value, Path.Combine(nugetConfigFileFolder, expectedRepositoryPath));
            }
        }

        [Fact]
        public void GetValueEvaluatesEnvironmentVariableWithAbsolutePath()
        {
            //Arrange
            var expectedRepositoryPath = @"C:\log\two";

            using (var nugetConfigFileFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var nugetConfigFile = "NuGet.config";
                var nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, nugetConfigFile);

                File.WriteAllText(nugetConfigFilePath, DefaultNuGetConfigurationWithEnvironmentVariable);

                Environment.SetEnvironmentVariable("RP_ENV_VAR", @"C:\log");

                //Act
                ISettings settings = new Settings(nugetConfigFileFolder, nugetConfigFile);

                //Assert
                Assert.Equal(settings.GetValue("config", "repositoryPath", isPath: true), expectedRepositoryPath);
            }
        }

        [Fact]
        public void GetSettingValuesEvaluatesEnvironmentVariableWithAbsolutePath()
        {
            //Arrange
            var expectedRepositoryPath = @"C:\log\two";

            using (var nugetConfigFileFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var nugetConfigFile = "NuGet.config";
                var nugetConfigFilePath = Path.Combine(nugetConfigFileFolder, nugetConfigFile);

                File.WriteAllText(nugetConfigFilePath, DefaultNuGetConfigurationWithEnvironmentVariable);

                Environment.SetEnvironmentVariable("RP_ENV_VAR", @"C:\log");

                //Act
                ISettings settings = new Settings(nugetConfigFileFolder, nugetConfigFile);

                //Assert
                var settingsForConfig = settings.GetSettingValues("config", isPath: true);
                Assert.Single(settingsForConfig);
                Assert.Equal(settingsForConfig.Single().Value, expectedRepositoryPath);
            }
        }
    }
}
