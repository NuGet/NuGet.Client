// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Internal.NuGet.Testing.SignedPackages;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.CommandLine.XPlat;
using NuGet.CommandLine.XPlat.ListPackage;
using NuGet.Commands;
using NuGet.Commands.Test;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.XPlat.FuncTest
{
    [Collection(XPlatCollection.Name)]
    public class ListPackageTests
    {
        private static readonly PackageSourceMapping NoPackageSourceMapping =
            new(new Dictionary<string, IReadOnlyList<string>>());
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
                    var result = testApp.Parse(argList.ToArray()).Invoke();

                    // Assert
                    mockCommandRunner.Verify();
                    Assert.NotNull(HttpHandlerResourceV3.CredentialService);
                    Assert.Equal(0, result);
                });
        }

        [Fact]
        public void BasicListPackageParsing_InteractiveTakesNoArguments_ReturnsNonZero()
        {
            VerifyCommand(
                (projectPath, mockCommandRunner, testApp, getLogLevel) =>
                {
                    // Arrange
                    // In System.CommandLine, passing extra unrecognized tokens results in a non-zero exit code
                    var argList = new List<string>() { "list", "--interactive", "no", projectPath };

                    // Act
                    var result = testApp.Parse(argList.ToArray()).Invoke();

                    // Assert
                    Assert.NotEqual(0, result);
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
                    var result = testApp.Parse(argList.ToArray()).Invoke();

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
                    var result = testApp.Parse(argList.ToArray()).Invoke();

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
                    var result = testApp.Parse(argList.ToArray()).Invoke();

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
                    var result = testApp.Parse(argList.ToArray()).Invoke();
                    Assert.NotEqual(0, result);
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
            solution.Create();

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
            ListPackageCommandRunner listPackageCommandRunner = new(new MSBuildAPIUtility(logger, virtualProjectBuilder: null));
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
                                        auditSources: null,
                                        logger: logger,
                                        cancellationToken: CancellationToken.None,
                                        packageSourceMapping: NoPackageSourceMapping);

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
        }

        static async Task RestoreProjectsAsync(SimpleTestPathContext pathContext, SimpleTestProjectContext projectA, SimpleTestProjectContext projectB, ITestOutputHelper testOutputHelper)
        {
            var settings = Settings.LoadDefaultSettings(Path.GetDirectoryName(pathContext.SolutionRoot), Path.GetFileName(pathContext.NuGetConfig), null);
            var packageSourceProvider = new PackageSourceProvider(settings);

            var sources = packageSourceProvider.LoadPackageSources();

            await RestoreProjectAsync(settings, pathContext, projectA, sources, testOutputHelper);
            await RestoreProjectAsync(settings, pathContext, projectB, sources, testOutputHelper);

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

        [InlineData(true)]
        [InlineData(false)]
        [Theory]
        public async Task CanListPackagesForProjectsInSolutions(bool useSlnx)
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

            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot, useSlnx);
            solution.Projects.Add(projectA);
            solution.Projects.Add(projectB);
            solution.Create();

            // List package command requires restore to be run before it can list packages.
            await RestoreProjectsAsync(pathContext, projectA, projectB, _testOutputHelper);

            var output = new StringBuilder();
            var error = new StringBuilder();
            using TextWriter consoleOut = new StringWriter(output);
            using TextWriter consoleError = new StringWriter(error);
            var logger = new TestLogger(_testOutputHelper);
            ListPackageCommandRunner listPackageCommandRunner = new(new MSBuildAPIUtility(logger, virtualProjectBuilder: null));
            var packageRefArgs = new ListPackageArgs(
                                        path: solution.SolutionPath,
                                        packageSources: [new PackageSource(pathContext.PackageSource)],
                                        frameworks: ["net6.0"],
                                        reportType: ReportType.Outdated,
                                        renderer: new ListPackageConsoleRenderer(consoleOut, consoleError),
                                        includeTransitive: false,
                                        prerelease: false,
                                        highestPatch: false,
                                        highestMinor: false,
                                        auditSources: null,
                                        logger: logger,
                                        cancellationToken: CancellationToken.None,
                                        packageSourceMapping: NoPackageSourceMapping);

            int result = await listPackageCommandRunner.ExecuteCommandAsync(packageRefArgs);
            Assert.True(result == 0, userMessage: logger.ShowMessages());
        }

        [Fact]
        public async Task CanListPackagesForFileBasedApp()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();

            var packageA100 = new SimpleTestPackageContext("A", "1.0.0");

            await SimpleTestPackageUtility.CreatePackagesAsync(
                pathContext.PackageSource,
                packageA100);

            var projectA = XPlatTestUtils.CreateProject("ProjectA", pathContext, "net6.0", fileBasedApp: true);
            projectA.AddPackageToAllFrameworks(packageA100);
            var projectB = SimpleTestProjectContext.CreateNETCore("ProjectB", pathContext.SolutionRoot, "net6.0");

            projectA.Save();
            projectB.Save();

            // List package command requires restore to be run before it can list packages.
            await RestoreProjectsAsync(pathContext, projectA, projectB, _testOutputHelper);

            var output = new StringBuilder();
            var error = new StringBuilder();
            using TextWriter consoleOut = new StringWriter(output);
            using TextWriter consoleError = new StringWriter(error);
            var logger = new TestLogger(_testOutputHelper);
            using var builder = TestVirtualProjectBuilder.From(projectA);
            ListPackageCommandRunner listPackageCommandRunner = new(new MSBuildAPIUtility(logger, builder));
            var packageRefArgs = new ListPackageArgs(
                                        path: builder.FilePath,
                                        packageSources: [new PackageSource(pathContext.PackageSource)],
                                        frameworks: ["net6.0"],
                                        reportType: ReportType.Outdated,
                                        renderer: new ListPackageConsoleRenderer(consoleOut, consoleError),
                                        includeTransitive: false,
                                        prerelease: false,
                                        highestPatch: false,
                                        highestMinor: false,
                                        auditSources: null,
                                        logger: logger,
                                        cancellationToken: CancellationToken.None,
                                        packageSourceMapping: NoPackageSourceMapping);

            int result = await listPackageCommandRunner.ExecuteCommandAsync(packageRefArgs);
            Assert.True(result == 0, userMessage: $"{output}\n{error}\n{logger.ShowMessages()}");
        }

        [Fact]
        public async Task GetReportDataAsync_WhenReportTypeIsVulnerable_ShouldUseAuditSources()
        {
            // Arrange
            using var mockServer = SetupMockServer();
            var auditSource = new PackageSource(mockServer.Uri + "v3/index.json") { AllowInsecureConnections = true };

            var mockRenderer = new Mock<IReportRenderer>();
            var mockLogger = new Mock<ILogger>();

            using var pathContext = new SimpleTestPathContext();
            var project = SetupTestProject(pathContext);
            SetupAssetsAndProps(project);

            var listPackageArgs = new ListPackageArgs(
                path: project.ProjectPath,
                packageSources: new List<PackageSource> { new PackageSource(pathContext.PackageSource) },
                frameworks: new List<string>(),
                ReportType.Vulnerable,
                mockRenderer.Object,
                includeTransitive: true,
                prerelease: false,
                highestPatch: false,
                highestMinor: false,
                new List<PackageSource> { auditSource },
                mockLogger.Object,
                CancellationToken.None,
                NoPackageSourceMapping
            );

            var listPackageCommandRunner = new ListPackageCommandRunner(new MSBuildAPIUtility(mockLogger.Object, virtualProjectBuilder: null));


            // Act
            var result = await listPackageCommandRunner.GetReportDataAsync(listPackageArgs);

            // Assert
            Assert.Equal(1, result.Item2.Projects.Count);
            Assert.Equal(1, result.Item2.Projects.First().TargetFrameworkPackages.Count);
            Assert.Equal(1, result.Item2.Projects.First().TargetFrameworkPackages.First().TopLevelPackages.Count);
            Assert.Equal(1, result.Item2.Projects.First().TargetFrameworkPackages.First().TopLevelPackages.First().Vulnerabilities.Count);
            Assert.Equal("0.0.9", result.Item2.Projects.First().TargetFrameworkPackages.First().TopLevelPackages.First().RequestedVersion);
            Assert.Equal("1.0.0", result.Item2.Projects.First().TargetFrameworkPackages.First().TopLevelPackages.First().ResolvedVersion);
            Assert.Equal(2, result.Item2.Projects[0].TargetFrameworkPackages[0].TopLevelPackages.First().Vulnerabilities.First().Severity);
            Assert.Equal("https://test/", result.Item2.Projects[0].TargetFrameworkPackages[0].TopLevelPackages.First().Vulnerabilities.First().AdvisoryUrl.ToString());
        }

        [Fact]
        public async Task GetReportDataAsync_WithSolutionFilePassed_ShouldList()
        {
            // Arrange
            using var mockServer = SetupMockServer();
            var auditSource = new PackageSource(mockServer.Uri + "v3/index.json") { AllowInsecureConnections = true };

            var mockRenderer = new Mock<IReportRenderer>();
            var mockLogger = new Mock<ILogger>();

            using var pathContext = new SimpleTestPathContext();
            var solution = SetupTestSolution(pathContext);
            SetupAssetsAndProps(solution.Projects[0]);

            var listPackageArgs = new ListPackageArgs(
                path: solution.SolutionPath,
                packageSources: new List<PackageSource> { new PackageSource(pathContext.PackageSource) },
                frameworks: new List<string>(),
                ReportType.Vulnerable,
                mockRenderer.Object,
                includeTransitive: true,
                prerelease: false,
                highestPatch: false,
                highestMinor: false,
                new List<PackageSource> { auditSource },
                mockLogger.Object,
                CancellationToken.None,
                NoPackageSourceMapping
            );

            var listPackageCommandRunner = new ListPackageCommandRunner(new MSBuildAPIUtility(mockLogger.Object, virtualProjectBuilder: null));

            // Act
            var result = await listPackageCommandRunner.GetReportDataAsync(listPackageArgs);

            // Assert
            Assert.Equal(1, result.Item2.Projects.Count);
            Assert.Equal(1, result.Item2.Projects.First().TargetFrameworkPackages.Count);
            Assert.Equal(1, result.Item2.Projects.First().TargetFrameworkPackages.First().TopLevelPackages.Count);
            Assert.Equal(1, result.Item2.Projects.First().TargetFrameworkPackages.First().TopLevelPackages.First().Vulnerabilities.Count);
            Assert.Equal("0.0.9", result.Item2.Projects.First().TargetFrameworkPackages.First().TopLevelPackages.First().RequestedVersion);
            Assert.Equal("1.0.0", result.Item2.Projects.First().TargetFrameworkPackages.First().TopLevelPackages.First().ResolvedVersion);
            Assert.Equal(2, result.Item2.Projects[0].TargetFrameworkPackages[0].TopLevelPackages.First().Vulnerabilities.First().Severity);
            Assert.Equal("https://test/", result.Item2.Projects[0].TargetFrameworkPackages[0].TopLevelPackages.First().Vulnerabilities.First().AdvisoryUrl.ToString());
        }

        [Fact]
        public async Task GetReportDataAsync_WhenReportTypeIsVulnerableAuditSourcesWithNoVulnerabilityInfoResource_ShouldWarn()
        {
            // Arrange
            const string indexJson = """
    {
        "version": "3.0.0",
        "resources": [{}]
    }
    """;

            using var mockServer = new MockServer();
            mockServer.Get.Add("/v3/index.json", _ => indexJson);
            mockServer.Start();

            var auditSource = new PackageSource($"{mockServer.Uri}v3/index.json") { AllowInsecureConnections = true };

            using var pathContext = new SimpleTestPathContext();
            var project = SetupTestProject(pathContext);
            SetupAssetsAndProps(project);

            var mockRenderer = new Mock<IReportRenderer>();
            var mockLogger = new Mock<ILogger>();

            var listPackageArgs = new ListPackageArgs(
                project.ProjectPath,
                new List<PackageSource> { new PackageSource(pathContext.PackageSource) },
                new List<string>(),
                ReportType.Vulnerable,
                mockRenderer.Object,
                includeTransitive: true,
                prerelease: false,
                highestPatch: false,
                highestMinor: false,
                new List<PackageSource> { auditSource },
                mockLogger.Object,
                CancellationToken.None,
                NoPackageSourceMapping
            );

            var listPackageCommandRunner = new ListPackageCommandRunner(new MSBuildAPIUtility(mockLogger.Object, virtualProjectBuilder: null));

            // Act
            var result = await listPackageCommandRunner.GetReportDataAsync(listPackageArgs);
            var projectResult = result.Item2.Projects.First();
            var warning = projectResult.ProjectProblems.First();

            // Assert
            Assert.Single(result.Item2.Projects);
            Assert.Single(projectResult.ProjectProblems);
            Assert.Equal(ProblemType.Warning, warning.ProblemType);
            Assert.Equal(
                string.Format(CultureInfo.CurrentCulture, CommandLine.XPlat.Strings.Warning_AuditSourceWithoutData, auditSource.Name),
                warning.Text
            );
            Assert.Empty(projectResult.TargetFrameworkPackages.First().TopLevelPackages);
            Assert.Empty(projectResult.TargetFrameworkPackages.First().TransitivePackages);
        }


        [Theory]
        [InlineData("--outdated")]
        [InlineData("--deprecated")]
        [InlineData("--vulnerable")]
        public void BasicListPackageParsing_SponsorCombinedWithAnotherReport_ReturnsNonZero(string otherReportOption)
        {
            VerifyCommand(
                (projectPath, mockCommandRunner, testApp, getLogLevel) =>
                {
                    // Act
                    var result = testApp.Parse(new[] { "list", projectPath, "--sponsor", otherReportOption }).Invoke();

                    // Assert
                    Assert.NotEqual(0, result);
                });
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public async Task SponsorReport_UsesRegistrationProviderPipeline(
            bool sourceSupportsSponsorship,
            bool sourceReturnsSponsorshipUrls)
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();
            await SimpleTestPackageUtility.CreatePackagesAsync(
                pathContext.PackageSource,
                new SimpleTestPackageContext("task", "1.0.0"));
            SimpleTestProjectContext project = SetupTestProject(pathContext);
            SetupAssetsAndProps(project);

            using var mockServer = new FileSystemBackedV3MockServer(
                pathContext.PackageSource,
                sourceSupportsSponsorship: sourceSupportsSponsorship);
            string[] expectedSponsorshipUrls = { "https://sponsor.test/task" };
            if (sourceReturnsSponsorshipUrls)
            {
                mockServer.SponsorshipUrls["task"] = expectedSponsorshipUrls;
            }
            mockServer.Start();

            var source = new PackageSource(mockServer.ServiceIndexUri, "test")
            {
                AllowInsecureConnections = true
            };
            var logger = new TestLogger(_testOutputHelper);
            var runner = new ListPackageCommandRunner(
                new MSBuildAPIUtility(logger, virtualProjectBuilder: null));
            ListPackageArgs args = CreateSponsorArgs(
                project.ProjectPath,
                new List<PackageSource> { source },
                Mock.Of<IReportRenderer>(),
                logger);

            // Act
            (int exitCode, ListPackageReportModel report) = await runner.GetReportDataAsync(args);

            // Assert
            Assert.Equal(0, exitCode);
            ListPackageProjectModel projectReport = Assert.Single(report.Projects);
            if (sourceSupportsSponsorship)
            {
                Assert.Equal(source, Assert.Single(projectReport.SponsorshipQueriedSources));
                Assert.Empty(projectReport.SponsorshipUnsupportedSources);
                Assert.Equal(1, mockServer.RegistrationRequestCount);
            }
            else
            {
                Assert.Empty(projectReport.SponsorshipQueriedSources);
                Assert.Equal(source, Assert.Single(projectReport.SponsorshipUnsupportedSources));
                Assert.Equal(0, mockServer.RegistrationRequestCount);
            }

            List<ListReportPackage> packages = projectReport.TargetFrameworkPackages
                .SelectMany(framework =>
                    (framework.TopLevelPackages ?? new List<ListReportPackage>())
                        .Concat(framework.TransitivePackages ?? new List<ListReportPackage>()))
                .ToList();
            if (sourceReturnsSponsorshipUrls)
            {
                ListReportPackage package = Assert.Single(packages);
                PackageSponsorship sponsorship = Assert.Single(package.Sponsorships);
                Assert.Equal(source.Source, sponsorship.Source);
                Assert.Equal(expectedSponsorshipUrls, sponsorship.Urls);
            }
            else
            {
                Assert.Empty(packages);
            }
        }

        [Fact]
        public async Task SponsorReport_PackageSourceMappingRequestsOnlyMappedSource()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();
            await SimpleTestPackageUtility.CreatePackagesAsync(
                pathContext.PackageSource,
                new SimpleTestPackageContext("task", "1.0.0"));
            SimpleTestProjectContext project = SetupTestProject(pathContext);
            SetupAssetsAndProps(project);

            using var mappedServer = new FileSystemBackedV3MockServer(
                pathContext.PackageSource,
                sourceSupportsSponsorship: true);
            using var unmappedServer = new FileSystemBackedV3MockServer(
                pathContext.PackageSource,
                sourceSupportsSponsorship: true);
            mappedServer.SponsorshipUrls["task"] = new[] { "https://sponsor.test/mapped" };
            unmappedServer.SponsorshipUrls["task"] = new[] { "https://sponsor.test/unmapped" };
            mappedServer.Start();
            unmappedServer.Start();

            var mappedSource = new PackageSource(mappedServer.ServiceIndexUri, "mapped")
            {
                AllowInsecureConnections = true
            };
            var unmappedSource = new PackageSource(unmappedServer.ServiceIndexUri, "unmapped")
            {
                AllowInsecureConnections = true
            };
            var sourceMapping = new PackageSourceMapping(
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["mapped"] = new[] { "task" },
                    ["unmapped"] = new[] { "Other.Package" },
                });
            var logger = new TestLogger(_testOutputHelper);
            var runner = new ListPackageCommandRunner(
                new MSBuildAPIUtility(logger, virtualProjectBuilder: null));
            ListPackageArgs args = CreateSponsorArgs(
                project.ProjectPath,
                new List<PackageSource> { mappedSource, unmappedSource },
                Mock.Of<IReportRenderer>(),
                logger,
                sourceMapping);

            // Act
            (int exitCode, ListPackageReportModel report) = await runner.GetReportDataAsync(args);

            // Assert
            Assert.Equal(0, exitCode);
            ListPackageProjectModel projectReport = Assert.Single(report.Projects);
            Assert.Equal(mappedSource, Assert.Single(projectReport.SponsorshipQueriedSources));
            Assert.Empty(projectReport.SponsorshipUnsupportedSources);
            Assert.Equal(1, mappedServer.RegistrationRequestCount);
            Assert.Equal(0, unmappedServer.RegistrationRequestCount);
        }

        [Theory]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(true, false, true)]
        public void ConsoleRenderer_Sponsor_WritesAccurateSourceDiagnosticsAndHint(
            bool hasSponsoredPackage,
            bool hasExplicitSources,
            bool shouldWriteSourceHint)
        {
            // Arrange
            var successfulSource = new PackageSource("https://successful.test/v3/index.json");
            var firstEmptySource = new PackageSource("https://empty-1.test/v3/index.json");
            var secondEmptySource = new PackageSource("https://empty-2.test/v3/index.json");
            var unsupportedSource = new PackageSource("https://unsupported.test/v3/index.json");
            var neverQueriedSource = new PackageSource("https://never-queried.test/v3/index.json");
            var packageSources = new List<PackageSource>
            {
                successfulSource,
                firstEmptySource,
                secondEmptySource,
                unsupportedSource,
                neverQueriedSource
            };
            var output = new StringBuilder();
            var error = new StringBuilder();
            using TextWriter consoleOut = new StringWriter(output);
            using TextWriter consoleError = new StringWriter(error);

            var renderer = new ListPackageConsoleRenderer(consoleOut, consoleError)
            {
                ShowSponsorshipSourceHint = !hasExplicitSources
            };
            ListPackageArgs listPackageArgs = CreateSponsorArgs(
                string.Empty,
                packageSources,
                renderer,
                NullLogger.Instance);

            var reportModel = new ListPackageReportModel(listPackageArgs);
            var projectModel = new ListPackageProjectModel("projectA.csproj", "ProjectA")
            {
                TargetFrameworkPackages = new List<ListPackageReportFrameworkPackage>
                {
                    new ListPackageReportFrameworkPackage("net8.0", "net8.0")
                    {
                        TopLevelPackages = hasSponsoredPackage
                            ? new List<ListReportPackage>
                            {
                                new ListReportPackage(
                                    packageId: "Sponsored.Package",
                                    resolvedVersion: "1.0.0",
                                    latestVersion: null,
                                    vulnerabilities: null,
                                    deprecationReasons: null,
                                    alternativePackage: null,
                                    requestedVersion: "1.0.0",
                                    autoReference: false,
                                    sponsorships: new[]
                                    {
                                        new PackageSponsorship(successfulSource.Source, new[] { "https://sponsor.test" })
                                    })
                            }
                            : null
                    }
                },
                SponsorshipQueriedSources = hasSponsoredPackage
                    ? new[] { secondEmptySource, successfulSource, firstEmptySource }
                    : new[] { secondEmptySource, firstEmptySource },
                SponsorshipUnsupportedSources = new[] { unsupportedSource },
            };
            reportModel.Projects.Add(projectModel);

            // Act
            renderer.Render(reportModel);
            string rendered = output.ToString();

            // Assert
            if (hasSponsoredPackage)
            {
                Assert.DoesNotContain("Project 'ProjectA' has no sponsorable packages.", rendered);
            }
            else
            {
                Assert.Contains("Project 'ProjectA' has no sponsorable packages.", rendered);
            }
            int noDetailsStart = rendered.IndexOf(CommandLine.XPlat.Strings.ListPkg_SponsorNoDetailsHeader, StringComparison.Ordinal);
            int unsupportedStart = rendered.IndexOf(CommandLine.XPlat.Strings.ListPkg_SponsorUnsupportedSourcesHeader, StringComparison.Ordinal);
            Assert.True(noDetailsStart >= 0);
            Assert.True(unsupportedStart > noDetailsStart);
            string noDetailsSection = rendered.Substring(noDetailsStart, unsupportedStart - noDetailsStart);
            string unsupportedSection = rendered.Substring(unsupportedStart);
            Assert.Contains(firstEmptySource.Source, noDetailsSection);
            Assert.Contains(secondEmptySource.Source, noDetailsSection);
            Assert.True(
                noDetailsSection.IndexOf(firstEmptySource.Source, StringComparison.Ordinal) <
                noDetailsSection.IndexOf(secondEmptySource.Source, StringComparison.Ordinal));
            Assert.DoesNotContain(successfulSource.Source, noDetailsSection);
            Assert.DoesNotContain(unsupportedSource.Source, noDetailsSection);
            Assert.Contains(unsupportedSource.Source, unsupportedSection);
            Assert.DoesNotContain(neverQueriedSource.Source, unsupportedSection);
            if (shouldWriteSourceHint)
            {
                Assert.Contains(CommandLine.XPlat.Strings.ListPkg_SponsorSourceHint, unsupportedSection);
            }
            else
            {
                Assert.DoesNotContain(CommandLine.XPlat.Strings.ListPkg_SponsorSourceHint, unsupportedSection);
            }
        }

        [Theory]
        [InlineData(true, false, "", true, false)]
        [InlineData(true, false, "--verbosity minimal", true, false)]
        [InlineData(true, false, "--verbosity normal", true, true)]
        [InlineData(true, false, "--verbosity normal --format json", true, false)]
        [InlineData(true, true, "", false, false)]
        [InlineData(true, true, "--format json", false, false)]
        [InlineData(false, true, "", true, false)]
        public void ListPackage_SourceConfiguration_ValidatesSponsorAndLogsOnlyForConsoleInformation(
            bool mappingEnabled,
            bool hasExplicitSource,
            string additionalOptions,
            bool shouldRun,
            bool shouldLogMappingNotice)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                ConfigurePackageSource(pathContext, mappingEnabled);

                VerifyCommand((projectPath, mockCommandRunner, testApp, getLogLevel, logger, output, error) =>
                {
                    // Arrange
                    ListPackageArgs capturedArgs = null;
                    mockCommandRunner
                        .Setup(m => m.ExecuteCommandAsync(It.IsAny<ListPackageArgs>()))
                        .Callback<ListPackageArgs>(args => capturedArgs = args)
                        .Returns(Task.FromResult(0));

                    var argList = new List<string> { "list", projectPath, "--sponsor", "--config", pathContext.NuGetConfig };
                    if (hasExplicitSource)
                    {
                        argList.AddRange(new[] { "--source", "mapped" });
                    }
                    if (additionalOptions.Length > 0)
                    {
                        argList.AddRange(additionalOptions.Split(' '));
                    }

                    // Act
                    var result = testApp.Parse(argList.ToArray()).Invoke();

                    // Assert
                    if (!shouldRun)
                    {
                        Assert.NotEqual(0, result);
                        if (additionalOptions.Contains("--format json", StringComparison.Ordinal))
                        {
                            JObject json = JObject.Parse(output.ToString());
                            Assert.Contains(
                                json["problems"],
                                problem => problem["text"].Value<string>() == CommandLine.XPlat.Strings.ListPkg_SponsorPackageSourceMappingWithSource);
                        }
                        else
                        {
                            Assert.Contains(CommandLine.XPlat.Strings.ListPkg_SponsorPackageSourceMappingWithSource, error.ToString());
                        }
                        Assert.DoesNotContain(CommandLine.XPlat.Strings.ListPkg_SponsorPackageSourceMappingEnabled, logger.ShowMessages());
                        mockCommandRunner.Verify(m => m.ExecuteCommandAsync(It.IsAny<ListPackageArgs>()), Times.Never);
                        return;
                    }

                    Assert.Equal(0, result);
                    Assert.NotNull(capturedArgs);
                    Assert.Equal(ReportType.Sponsor, capturedArgs.ReportType);
                    Assert.NotNull(capturedArgs.PackageSourceMapping);
                    Assert.Equal(mappingEnabled, capturedArgs.PackageSourceMapping.IsEnabled);
                    string[] expectedSources = hasExplicitSource
                        ? new[] { "mapped", "source" }
                        : new[] { "source", "mapped" };
                    Assert.Equal(expectedSources, capturedArgs.PackageSources.Select(source => source.Name));
                    Assert.All(
                        capturedArgs.PackageSources.Where(source => source.Name == "mapped"),
                        source => Assert.Equal("https://mapped.test/v3/index.json", source.Source));
                    if (capturedArgs.Renderer is ListPackageConsoleRenderer capturedRenderer)
                    {
                        Assert.Equal(!hasExplicitSource, capturedRenderer.ShowSponsorshipSourceHint);
                    }
                    if (shouldLogMappingNotice)
                    {
                        Assert.Contains(CommandLine.XPlat.Strings.ListPkg_SponsorPackageSourceMappingEnabled, logger.ShowMessages());
                    }
                    else
                    {
                        Assert.DoesNotContain(CommandLine.XPlat.Strings.ListPkg_SponsorPackageSourceMappingEnabled, logger.ShowMessages());
                    }
                    mockCommandRunner.Verify(m => m.ExecuteCommandAsync(It.IsAny<ListPackageArgs>()), Times.Once);
                },
                observeLogLevel: true);
            }
        }

        private static void ConfigurePackageSource(SimpleTestPathContext pathContext, bool mappingEnabled)
        {
            pathContext.Settings.AddSource("mapped", "https://mapped.test/v3/index.json");
            if (mappingEnabled)
            {
                pathContext.Settings.AddPackageSourceMapping("mapped", "*");
            }
        }

        private void VerifyCommand(Action<string, Mock<IListPackageCommandRunner>, RootCommand, Func<LogLevel>> verify)
        {
            VerifyCommand((projectPath, mockCommandRunner, testApp, getLogLevel, logger, output, error) =>
                verify(projectPath, mockCommandRunner, testApp, getLogLevel));
        }

        private void VerifyCommand(
            Action<string, Mock<IListPackageCommandRunner>, RootCommand, Func<LogLevel>, TestCommandOutputLogger, StringBuilder, StringBuilder> verify,
            bool observeLogLevel = false)
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var projectPath = Path.Combine(testDirectory, "project.csproj");
                File.WriteAllText(projectPath, string.Empty);

                var logLevel = LogLevel.Information;
                var logger = new TestCommandOutputLogger(_testOutputHelper, observeLogLevel);
                var testApp = new RootCommand();
                var mockCommandRunner = new Mock<IListPackageCommandRunner>();
                var output = new StringBuilder();
                var error = new StringBuilder();
                using TextWriter consoleOut = new StringWriter(output);
                using TextWriter consoleError = new StringWriter(error);
                mockCommandRunner
                    .Setup(m => m.ExecuteCommandAsync(It.IsAny<ListPackageArgs>()))
                    .Returns(Task.FromResult(0));

                ListPackageCommand.Register(testApp,
                    () => logger,
                    ll =>
                    {
                        logLevel = ll;
                        logger.VerbosityLevel = ll;
                    },
                    () => mockCommandRunner.Object,
                    consoleOut,
                    consoleError);

                // Act & Assert
                try
                {
                    verify(projectPath, mockCommandRunner, testApp, () => logLevel, logger, output, error);
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
            fields.Length.Should().Be(
                14,
                because: "Number of fields are changed in ListPackageArgs.cs. " +
                    "Please make sure this change is accounted for GetReportParameters method in that file.");
        }

        private static SimpleTestSolutionContext SetupTestSolution(SimpleTestPathContext pathContext)
        {
            var package = new SimpleTestPackageContext { Id = "task", Version = "0.0.9" };

            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
            var project = SimpleTestProjectContext.CreateNETCore("ProjectA", pathContext.SolutionRoot, NuGetFramework.Parse("net8.0"));
            project.Type = ProjectStyle.PackageReference;
            project.SingleTargetFramework = true;
            project.AddPackageToAllFrameworks(package);

            solution.Projects.Add(project);
            solution.Create();

            return solution;
        }

        private static SimpleTestProjectContext SetupTestProject(SimpleTestPathContext pathContext)
        {
            var package = new SimpleTestPackageContext { Id = "task", Version = "0.0.9" };

            var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
            var project = SimpleTestProjectContext.CreateNETCore("ProjectA", pathContext.SolutionRoot, NuGetFramework.Parse("net8.0"));
            project.Type = ProjectStyle.PackageReference;
            project.SingleTargetFramework = true;
            project.AddPackageToAllFrameworks(package);

            solution.Projects.Add(project);
            solution.Create();

            return project;
        }

        private void SetupAssetsAndProps(SimpleTestProjectContext project)
        {
            Directory.CreateDirectory(project.ProjectExtensionsPath);

            string assetsPath = Path.Combine(project.ProjectExtensionsPath, "project.assets.json");
            string propsPath = Path.Combine(project.ProjectExtensionsPath, "ProjectA.csproj.nuget.g.props");

            string assetsContent = ResourceTestUtility.GetResource(
                "NuGet.XPlat.FuncTest.compiler.resources.Test.OnePackage.project.assets.json",
                GetType()
            );
            string propsContent = ResourceTestUtility.GetResource(
                "NuGet.XPlat.FuncTest.compiler.resources.Test.ProjectA.csproj.nuget.g.props",
                GetType()
            );

            File.WriteAllText(assetsPath, assetsContent);
            File.WriteAllText(propsPath, propsContent);
        }

        private static ListPackageArgs CreateSponsorArgs(
            string projectPath,
            List<PackageSource> packageSources,
            IReportRenderer renderer,
            ILogger logger,
            PackageSourceMapping packageSourceMapping = null)
        {
            return new ListPackageArgs(
                path: projectPath,
                packageSources: packageSources,
                frameworks: new List<string>(),
                reportType: ReportType.Sponsor,
                renderer: renderer,
                includeTransitive: false,
                prerelease: false,
                highestPatch: false,
                highestMinor: false,
                auditSources: null,
                logger: logger,
                cancellationToken: CancellationToken.None,
                packageSourceMapping: packageSourceMapping ?? NoPackageSourceMapping);
        }

        private static MockServer SetupMockServer()
        {
            var mockServer = new MockServer();

            string indexJson = $@"
    {{
        ""version"": ""3.0.0"",
        ""resources"": [
            {{
                ""@id"": ""{mockServer.Uri}v3/vulnerabilities/index.json"",
                ""@type"": ""VulnerabilityInfo/6.7.0"",
                ""comment"": ""This is a test feed for vulnerabilities""
            }}
        ]
    }}";

            string vulnerabilitiesJson = $@"
    [
        {{
            ""@name"": ""base"",
            ""@id"": ""{mockServer.Uri}v3-vulnerabilities/2024.12.21.05.12.11/vulnerability.base.json"",
            ""@updated"": ""2024-12-21T05:12:11.2008556Z"",
            ""comment"": ""The base data for vulnerability update periodically""
        }}
    ]";

            string baseVulnerabilityJson = $@"
    {{
        ""task"": [
            {{
                ""url"": ""https://test/"",
                ""severity"": 2,
                ""versions"": ""(, 10.0.3)""
            }}
        ]
    }}";

            mockServer.Get.Add("/v3/index.json", _ => indexJson);
            mockServer.Get.Add("/v3/vulnerabilities/index.json", _ => vulnerabilitiesJson);
            mockServer.Get.Add("/v3-vulnerabilities/2024.12.21.05.12.11/vulnerability.base.json", _ => baseVulnerabilityJson);
            mockServer.Start();

            return mockServer;
        }

    }
}
