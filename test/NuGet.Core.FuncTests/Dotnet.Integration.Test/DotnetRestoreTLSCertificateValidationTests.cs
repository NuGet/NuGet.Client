// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Test.Utility;
using NuGet.XPlat.FuncTest;
using Test.Utility;
using Xunit;

namespace Dotnet.Integration.Test
{
    [Collection(DotnetIntegrationCollection.Name)]
    public class DotnetRestoreTLSCertificateValidationTests
    {
        private readonly DotnetIntegrationTestFixture _msbuildFixture;

        public DotnetRestoreTLSCertificateValidationTests(DotnetIntegrationTestFixture fixture)
        {
            _msbuildFixture = fixture;
        }

        [PlatformFact(Platform.Windows)]
        public async Task DotnetRestore_withTLSCertificateValidationDisabled_DoesnotThrowException()
        {
            // Arrange
            using var pathContext = _msbuildFixture.CreateSimpleTestPathContext();
            var packageA100 = new SimpleTestPackageContext("A", "1.0.0");
            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageA100);
            var projectA = XPlatTestUtils.CreateProject("ProjectA", pathContext, packageA100, "net472");
            var workingDirectory = Path.Combine(pathContext.SolutionRoot, projectA.ProjectName);
            SelfSignedCertificateMockServer tcpListenerServer = new SelfSignedCertificateMockServer(pathContext.PackageSource);
            var serverTask = tcpListenerServer.StartServer();
            var configFile = @$"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""source1"" value=""{tcpListenerServer.URI}v3/index.json"" disableTLSCertificateValidation=""true""/>
    </packageSources>
</configuration>
";

            // Act & Assert
            File.WriteAllText(Path.Combine(workingDirectory, "NuGet.Config"), configFile);
            _msbuildFixture.RunDotnetExpectSuccess(workingDirectory, $"restore {projectA.ProjectName}.csproj --configfile ./NuGet.config");
            tcpListenerServer.StopServer();
        }

        [PlatformFact(Platform.Windows)]
        public async Task DotnetRestore_withTLSCertificateValidationEnabled_ThrowException()
        {
            // Arrange
            using var pathContext = _msbuildFixture.CreateSimpleTestPathContext();
            var packageB100 = new SimpleTestPackageContext("myPackg", "1.0.0");
            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageB100);
            var projectB = XPlatTestUtils.CreateProject("ProjectB", pathContext, packageB100, "net472");
            var workingDirectory = Path.Combine(pathContext.SolutionRoot, projectB.ProjectName);
            SelfSignedCertificateMockServer tcpListenerServer = new SelfSignedCertificateMockServer(pathContext.PackageSource);
            var serverTask = tcpListenerServer.StartServer();
            var configFile = @$"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""source1"" value=""{tcpListenerServer.URI}v3/index.json""/>
    </packageSources>
</configuration>
";
            File.WriteAllText(Path.Combine(workingDirectory, "NuGet.Config"), configFile);

            // Act & Assert
            var _result = _msbuildFixture.RunDotnetExpectFailure(workingDirectory, $"restore {projectB.ProjectName}.csproj --configfile ./NuGet.config");
            Assert.Contains("SSL connection could not be established", _result.AllOutput);
            tcpListenerServer.StopServer();
        }

        [PlatformFact(Platform.Windows)]
        public async Task DotnetRestore_withAnotherSourceTLSCertificateValidationDisbaled_ThrowException()
        {
            // Arrange
            using var pathContext = _msbuildFixture.CreateSimpleTestPathContext();
            var packageB100 = new SimpleTestPackageContext("myPackg", "1.0.0");
            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageB100);
            var projectB = XPlatTestUtils.CreateProject("ProjectB", pathContext, packageB100, "net472");
            var workingDirectory = Path.Combine(pathContext.SolutionRoot, projectB.ProjectName);
            SelfSignedCertificateMockServer tcpListenerServer1 = new SelfSignedCertificateMockServer(pathContext.PackageSource);
            SelfSignedCertificateMockServer tcpListenerServer2 = new SelfSignedCertificateMockServer(pathContext.PackageSource);
            var serverTask = tcpListenerServer1.StartServer();
            var serverTask2 = tcpListenerServer2.StartServer();
            var configFile = @$"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""source1"" value=""{tcpListenerServer1.URI}v3/index.json""/>
        <add key=""source2"" value=""{tcpListenerServer2.URI}v3/index.json"" disableTLSCertificateValidation=""true""/>
    </packageSources>
</configuration>
";
            File.WriteAllText(Path.Combine(workingDirectory, "NuGet.Config"), configFile);

            // Act & Assert
            var _result = _msbuildFixture.RunDotnetExpectFailure(workingDirectory, $"restore {projectB.ProjectName}.csproj --configfile ./NuGet.config");
            Assert.Contains("SSL connection could not be established", _result.AllOutput);
            tcpListenerServer1.StopServer();
            tcpListenerServer2.StopServer();
        }

        [PlatformFact(Platform.Windows)]
        public async Task DotnetRestore_withAnotherSourceTLSCertificateValidationEnabled_DoesNotThrowException()
        {
            // Arrange
            using var pathContext = _msbuildFixture.CreateSimpleTestPathContext();
            var packageB100 = new SimpleTestPackageContext("myPackg", "1.0.0");
            await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageB100);
            var projectB = XPlatTestUtils.CreateProject("ProjectB", pathContext, packageB100, "net472");
            var workingDirectory = Path.Combine(pathContext.SolutionRoot, projectB.ProjectName);
            SelfSignedCertificateMockServer tcpListenerServer1 = new SelfSignedCertificateMockServer(pathContext.PackageSource);
            SelfSignedCertificateMockServer tcpListenerServer2 = new SelfSignedCertificateMockServer(pathContext.PackageSource);
            var serverTask = tcpListenerServer1.StartServer();
            var serverTask2 = tcpListenerServer2.StartServer();
            var configFile = @$"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""source1"" value=""{tcpListenerServer1.URI}v3/index.json""/>
        <add key=""source2"" value=""{tcpListenerServer2.URI}v3/index.json"" disableTLSCertificateValidation=""true""/>
    </packageSources>
</configuration>
";
            File.WriteAllText(Path.Combine(workingDirectory, "NuGet.Config"), configFile);

            // Act & Assert
            var _result = _msbuildFixture.RunDotnetExpectSuccess(workingDirectory, $"restore {projectB.ProjectName}.csproj --configfile ./NuGet.config --source {tcpListenerServer2.URI}v3/index.json");
            tcpListenerServer1.StopServer();
            tcpListenerServer2.StopServer();
        }
    }
}
