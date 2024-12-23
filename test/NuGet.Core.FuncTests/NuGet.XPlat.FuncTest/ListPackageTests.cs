// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.CommandLineUtils;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.CommandLine.XPlat.ListPackage;
using NuGet.Commands;
using NuGet.Commands.Test;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.XPlat.FuncTest
{
    [Collection("NuGet XPlat Test Collection")]
    public class ListPackageTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public ListPackageTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void BasicListPackageParsing_Interactive()
        {
            VerifyCommand(
                (projectPath, mockCommandRunner, testApp, getLogLevel) =>
                {
                    // Arrange
                    var argList = new List<string> { "list", "--interactive", projectPath };

                    // Act
                    var result = testApp.Execute(argList.ToArray());

                    // Assert
                    mockCommandRunner.Verify();
                    Assert.NotNull(HttpHandlerResourceV3.CredentialService);
                    Assert.Equal(0, result);
                });
        }

        [Fact]
        public void BasicListPackageParsing_InteractiveTakesNoArguments_ThrowsException()
        {
            VerifyCommand(
                (projectPath, mockCommandRunner, testApp, getLogLevel) =>
                {
                    // Arrange
                    var argList = new List<string>() { "list", "--interactive", "no", projectPath };

                    // Act & Assert
                    Assert.Throws<CommandParsingException>(() => testApp.Execute(argList.ToArray()));
                });
        }

        [Theory]
        [InlineData("q", LogLevel.Warning)]
        [InlineData("quiet", LogLevel.Warning)]
        [InlineData("m", LogLevel.Minimal)]
        [InlineData("minimal", LogLevel.Minimal)]
        [InlineData("something-else", LogLevel.Minimal)]
        [InlineData("n", LogLevel.Information)]
        [InlineData("normal", LogLevel.Information)]
        [InlineData("d", LogLevel.Debug)]
        [InlineData("detailed", LogLevel.Debug)]
        [InlineData("diag", LogLevel.Debug)]
        [InlineData("diagnostic", LogLevel.Debug)]
        public void BasicListPackageParsing_VerbosityOption(string verbosity, LogLevel logLevel)
        {
            VerifyCommand(
                (projectPath, mockCommandRunner, testApp, getLogLevel) =>
                {
                    // Arrange
                    var argList = new List<string> { "list", projectPath, "--verbosity", verbosity };

                    // Act
                    var result = testApp.Execute(argList.ToArray());

                    // Assert
                    Assert.Equal(logLevel, getLogLevel());
                    Assert.Equal(0, result);
                });
        }

        [Fact]
        public void BasicListPackageParsing_NoVerbosityOption()
        {
            VerifyCommand((projectPath, mockCommandRunner, testApp, getLogLevel) =>
                {
                    // Arrange
                    var argList = new List<string> { "list", projectPath };

                    // Act
                    var result = testApp.Execute(argList.ToArray());

                    // Assert
                    Assert.Equal(LogLevel.Minimal, getLogLevel());
                    Assert.Equal(0, result);
                });
        }

        [Theory]
        [InlineData("")]
        [InlineData("--format json")]
        [InlineData("--format JSON")]
        [InlineData("--format json --output-version 1")]
        [InlineData("--format console")]
        public void BasicListPackage_OutputFormat_CorrectInput_Parsing_Succeeds(string outputFormatCommmand)
        {
            VerifyCommand(
                (projectPath, mockCommandRunner, testApp, getLogLevel) =>
                {
                    // Arrange
                    var argList = new List<string>() { "list" };

                    if (!string.IsNullOrEmpty(outputFormatCommmand))
                    {
                        argList.AddRange(outputFormatCommmand.Split(' ').ToList());
                    }

                    argList.Add(projectPath);

                    // Act
                    var result = testApp.Execute(argList.ToArray());

                    // Assert
                    mockCommandRunner.Verify();
                    Assert.Equal(0, result);
                });
        }

        [Theory]
        [InlineData("--format xml")]
        [InlineData("--format json --output-version 0")]
        [InlineData("--format json --output-version 2")]
        [InlineData("--format console --output-version 1")]
        [InlineData("--output-version 0")]
        [InlineData("--output-version 1")]
        public void BasicListPackage_OutputFormat_BadInput_Parsing_Fails(string outputFormatCommmand)
        {
            VerifyCommand(
                (projectPath, mockCommandRunner, testApp, getLogLevel) =>
                {
                    // Arrange
                    var argList = new List<string>() { "list" };

                    if (!string.IsNullOrEmpty(outputFormatCommmand))
                    {
                        argList.AddRange(outputFormatCommmand.Split(' ').ToList());
                    }

                    argList.Add(projectPath);

                    // Act & Assert
                    Assert.Throws<AggregateException>(() => testApp.Execute(argList.ToArray()));
                });
        }

        [PlatformFact(Platform.Windows, Skip = "https://github.com/NuGet/Home/issues/13874")]
        public async Task ListPackage_WithPrivateHttpSourceCredentialServiceIsInvokedAsNeeded_Succeeds()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();

            var packageA100 = new SimpleTestPackageContext("A", "1.0.0");
            var packageB100 = new SimpleTestPackageContext("B", "1.0.0");

            await SimpleTestPackageUtility.CreatePackagesAsync(
                    pathContext.PackageSource,
                    packageA100,
                    packageB100);

            var projectA = SimpleTestProjectContext.CreateNETCore("ProjectA", pathContext.SolutionRoot, "net6.0");
            var projectB = SimpleTestProjectContext.CreateNETCore("ProjectB", pathContext.SolutionRoot, "net6.0");

            projectA.AddPackageToAllFrameworks(packageA100);
            projectB.AddPackageToAllFrameworks(packageB100);

            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
            solution.Projects.Add(projectA);
            solution.Projects.Add(projectB);
            solution.Create(pathContext.SolutionRoot);

            SimpleTestSettingsContext.RemoveSource(pathContext.Settings.XML, "source");

            using var mockServer = new FileSystemBackedV3MockServer(pathContext.PackageSource, isPrivateFeed: true);
            mockServer.Start();
            pathContext.Settings.AddSource(sourceName: "private-source", sourceUri: mockServer.ServiceIndexUri, allowInsecureConnectionsValue: bool.TrueString);

            var mockedCredentialService = new Mock<ICredentialService>();
            var expectedCredentials = new NetworkCredential("user", "password1");
            SetupCredentialServiceMock(mockedCredentialService, expectedCredentials, new Uri(mockServer.ServiceIndexUri));
            HttpHandlerResourceV3.CredentialService = new Lazy<ICredentialService>(() => mockedCredentialService.Object);

            // List package command requires restore to be run before it can list packages.
            await RestoreProjectsAsync(pathContext, projectA, projectB, _testOutputHelper);

            // Act
            var output = new StringBuilder();
            var error = new StringBuilder();
            using TextWriter consoleOut = new StringWriter(output);
            using TextWriter consoleError = new StringWriter(error);
            var logger = new TestLogger(_testOutputHelper);
            ListPackageCommandRunner listPackageCommandRunner = new();
            var packageRefArgs = new ListPackageArgs(
                                        path: Path.Combine(pathContext.SolutionRoot, "solution.sln"),
                                        packageSources: [new(mockServer.ServiceIndexUri)],
                                        frameworks: ["net6.0"],
                                        reportType: ReportType.Vulnerable,
                                        renderer: new ListPackageConsoleRenderer(consoleOut, consoleError),
                                        includeTransitive: false,
                                        prerelease: false,
                                        highestPatch: false,
                                        highestMinor: false,
                                        logger: logger,
                                        cancellationToken: CancellationToken.None);

            int result = await listPackageCommandRunner.ExecuteCommandAsync(packageRefArgs);

            // Assert
            Assert.True(result == 0, userMessage: logger.ShowMessages());
            // GetCredentialsAsync should be called once during restore
            mockedCredentialService.Verify(x => x.GetCredentialsAsync(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<CredentialRequestType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            // TryGetLastKnownGoodCredentialsFromCache should be called twice during restore and once during list package.Hence total 3 times.
            mockedCredentialService.Verify(x => x.TryGetLastKnownGoodCredentialsFromCache(It.IsAny<Uri>(), It.IsAny<bool>(), out It.Ref<ICredentials>.IsAny), Times.Exactly(3));

            static void SetupCredentialServiceMock(Mock<ICredentialService> mockedCredentialService, NetworkCredential expectedCredentials, Uri packageSourceUri)
            {
                NetworkCredential cachedCredentials = default;
                mockedCredentialService.SetupGet(x => x.HandlesDefaultCredentials).Returns(true);
                // Setup GetCredentialsAsync mock
                mockedCredentialService
                    .Setup(x => x.GetCredentialsAsync(packageSourceUri, It.IsAny<IWebProxy>(), CredentialRequestType.Unauthorized, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() =>
                    {
                        cachedCredentials = expectedCredentials;
                        return cachedCredentials;
                    });
                // Setup TryGetLastKnownGoodCredentialsFromCache mock
                mockedCredentialService
                    .Setup(x => x.TryGetLastKnownGoodCredentialsFromCache(packageSourceUri, It.IsAny<bool>(), out It.Ref<ICredentials>.IsAny))
                    .Returns((Uri sourceUri, bool isProxyRequest, out ICredentials outCredentials) =>
                    {
                        outCredentials = cachedCredentials;
                        return outCredentials != null;
                    });
            }

            static async Task RestoreProjectsAsync(SimpleTestPathContext pathContext, SimpleTestProjectContext projectA, SimpleTestProjectContext projectB, ITestOutputHelper testOutputHelper)
            {
                var settings = Settings.LoadDefaultSettings(Path.GetDirectoryName(pathContext.SolutionRoot), Path.GetFileName(pathContext.NuGetConfig), null);
                var packageSourceProvider = new PackageSourceProvider(settings);

                var sources = packageSourceProvider.LoadPackageSources();

                await RestoreProjectAsync(settings, pathContext, projectA, sources, testOutputHelper);
                await RestoreProjectAsync(settings, pathContext, projectB, sources, testOutputHelper);
            }

            static async Task RestoreProjectAsync(ISettings settings,
                SimpleTestPathContext pathContext,
                SimpleTestProjectContext project,
                IEnumerable<PackageSource> packageSources,
                ITestOutputHelper testOutputHelper)
            {
                var packageSpec = ProjectTestHelpers.WithSettingsBasedRestoreMetadata(project.PackageSpec, settings);

                var logger = new TestLogger(testOutputHelper);

                var command = new RestoreCommand(ProjectTestHelpers.CreateRestoreRequest(pathContext, logger, packageSpec));
                var restoreResult = await command.ExecuteAsync(CancellationToken.None);
                await restoreResult.CommitAsync(logger, CancellationToken.None);
                Assert.True(restoreResult.Success, userMessage: logger.ShowMessages());
            }
        }

        [Fact]
        public async Task ExecuteCommandAsync_ShouldUseAuditSources_WhenReportTypeIsVulnerable()
        {
            // Arrange
            var advisoryUrl = "https://test/";
            var severity = 2;
            var mockRenderer = new Mock<IReportRenderer>();
            var mockServer = new MockServer();
            string index = $@"{{
                    ""version"": ""3.0.0"",
                    ""resources"": [
                        {{
                            ""@id"": ""{mockServer.Uri}v3/vulnerabilities/index.json"",
                            ""@type"": ""VulnerabilityInfo/6.7.0"",
                            ""comment"": ""This is a test feed for vulnerabilities""
                        }}
                    ]
                }}";

            string vulnerabilities = $@"
[
    {{
        ""@name"": ""base"",
        ""@id"": ""{mockServer.Uri}v3-vulnerabilities/2024.12.21.05.12.11/vulnerability.base.json"",
        ""@updated"": ""2024-12-21T05:12:11.2008556Z"",
        ""comment"": ""The base data for vulnerability update periodically""
    }}
]";

            string baseVulnerability = $@"
{{
    ""mypkg"": [
        {{
            ""url"": ""{advisoryUrl}"",
            ""severity"": {severity},
            ""versions"": ""(, 5.8.4)""
        }}
    ]
}}
";
            mockServer.Get.Add("/v3/index.json", r => index);
            mockServer.Get.Add("/v3/vulnerabilities/index.json", r => vulnerabilities);
            mockServer.Get.Add("/v3-vulnerabilities/2024.12.21.05.12.11/vulnerability.base.json", r => baseVulnerability);
            mockServer.Start();

            var auditSource = new PackageSource(mockServer.Uri + "v3/index.json");
            auditSource.AllowInsecureConnections = true;

            using (SimpleTestPathContext pathContext = new SimpleTestPathContext())
            {
                string projectFolder = Path.Combine(pathContext.SolutionRoot, "MyProject");
                string csprojPath = Path.Combine(projectFolder, "MyProject.csproj");
                string objFolder = Path.Combine(projectFolder, "obj");
                string assetsPath = Path.Combine(objFolder, "project.assets.json");

                //setup package
                var packageContext = new SimpleTestPackageContext()
                {
                    Id = "mypkg",
                    Version = "5.8.2",
                    Nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>mypkg</id>
                            <version>5.8.2</version>
                            <title />
                            <frameworkAssemblies>
                                <frameworkAssembly assemblyName=""System.Runtime"" />
                            </frameworkAssemblies>
                            <contentFiles>
                                <files include=""lib/net45/mypkg.dll"" copyToOutput=""true"" flatten=""false"" />
                            </contentFiles>
                        </metadata>
                        </package>")
                };
                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, packageContext);

                // Define the content for csproj
                var csprojContent = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net5.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""mypkg"" Version=""5.8.2"" />
  </ItemGroup>
</Project>";

                // Define the content for assets.json
                var assetsContent = @"
{
  ""version"": 3,
  ""targets"": {
    "".NETCoreApp,Version=v5.0"": {
      ""mypkg/5.8.2"": {
        ""type"": ""package"",
        ""compile"": {
          ""lib/net5.0/mypkg.dll"": {}
        },
        ""runtime"": {
          ""lib/net5.0/mypkg.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""mypkg/5.8.2"": {
      ""sha512"": ""<hash-value>"",
      ""type"": ""package"",
      ""path"": ""mypkg/5.8.2""
    }
  },
  ""project"": {
    ""version"": ""5.0"",
    ""restore"": {
      ""projectStyle"": ""PackageReference""
    },
    ""frameworks"": {
      ""net5.0"": {
        ""dependencies"": {
          ""mypkg"": {
            ""version"": ""5.8.2"",
            ""type"": ""direct""
          }
        }
      }
    }
  }
}";
                if (!Directory.Exists(projectFolder))
                {
                    Directory.CreateDirectory(projectFolder);
                }

                if (!Directory.Exists(objFolder))
                {
                    Directory.CreateDirectory(objFolder);
                }

                File.WriteAllText(csprojPath, csprojContent);
                File.WriteAllText(assetsPath, assetsContent);

                var listPackageArgs = new ListPackageArgs(
                    path: csprojPath,
                    packageSources: new List<PackageSource>() { new PackageSource(pathContext.PackageSource) },
                    frameworks: new List<string>(),
                    ReportType.Vulnerable,
                    mockRenderer.Object,
                    includeTransitive: true, prerelease: false, highestPatch: false, highestMinor: false,
                    auditSources: new List<PackageSource> { auditSource },
                    logger: new Mock<ILogger>().Object,
                    CancellationToken.None);

                var listPackageCommandRunner = new ListPackageCommandRunner();


                // Act
                var result = await listPackageCommandRunner.GetReportDataAsync(listPackageArgs);

                // Assert
                Assert.Equal(1, result.Item2.Projects.Count);
                Assert.Equal(1, result.Item2.Projects.First().TargetFrameworkPackages.Count);
                Assert.Equal(1, result.Item2.Projects.First().TargetFrameworkPackages.First().TopLevelPackages.Count);
                Assert.Equal(1, result.Item2.Projects.First().TargetFrameworkPackages.First().TopLevelPackages.First().Vulnerabilities.Count);
                Assert.Equal(severity, result.Item2.Projects[0].TargetFrameworkPackages[0].TopLevelPackages.First().Vulnerabilities.First().Severity);
                Assert.Equal(advisoryUrl, result.Item2.Projects[0].TargetFrameworkPackages[0].TopLevelPackages.First().Vulnerabilities.First().AdvisoryUrl.ToString());

            }
        }

        private void VerifyCommand(Action<string, Mock<IListPackageCommandRunner>, CommandLineApplication, Func<LogLevel>> verify)
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var projectPath = Path.Combine(testDirectory, "project.csproj");
                File.WriteAllText(projectPath, string.Empty);

                var logLevel = LogLevel.Information;
                var logger = new TestCommandOutputLogger(_testOutputHelper);
                var testApp = new CommandLineApplication();
                var mockCommandRunner = new Mock<IListPackageCommandRunner>();
                mockCommandRunner
                    .Setup(m => m.ExecuteCommandAsync(It.IsAny<ListPackageArgs>()))
                    .Returns(Task.FromResult(0));

                testApp.Name = "dotnet nuget_test";
                ListPackageCommand.Register(testApp,
                    () => logger,
                    ll => logLevel = ll,
                    () => mockCommandRunner.Object);

                // Act & Assert
                try
                {
                    verify(projectPath, mockCommandRunner, testApp, () => logLevel);
                }
                finally
                {
                    XPlatTestUtils.DisposeTemporaryFile(projectPath);
                }
            }
        }

        [Fact]
        public void JsonRenderer_ListPackageArgse_Verify_AllFields_Covered()
        {
            Type listPackageArgsType = typeof(ListPackageArgs);
            FieldInfo[] fields = listPackageArgsType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.True(13 == fields.Length, "Number of fields are changed in ListPackageArgs.cs. Please make sure this change is accounted for GetReportParameters method in that file.");
        }
    }
}
