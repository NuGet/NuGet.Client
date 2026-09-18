// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.CommandLine.XPlat.ListPackage;
using NuGet.CommandLine.XPlat.Utility;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class ListPackageCommandRunnerTests
    {
        private static readonly PackageSourceMapping NoPackageSourceMapping =
            new(new Dictionary<string, IReadOnlyList<string>>());

        public class TopLevelPackagesFilterForOutdated
        {
            [Fact]
            public void FiltersAutoReferencedPackages()
            {
                // Arrange
                Func<InstalledPackageReference, bool> filter = ListPackageHelper.TopLevelPackagesFilterForOutdated;
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference(autoReference: true);

                // Act
                bool result = filter.Invoke(installedPackageReference);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public void DoesNotFilterPackagesWithLatestMetadataNull()
            {
                // Arrange
                Func<InstalledPackageReference, bool> filter = ListPackageHelper.TopLevelPackagesFilterForOutdated;
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference();
                installedPackageReference.LatestPackageMetadata = null;

                // Act
                bool result = filter.Invoke(installedPackageReference);

                // Assert
                Assert.True(result);
            }

            [Fact]
            public void DoesNotFilterPackagesWithNewerVersionAvailable()
            {
                // Arrange
                Func<InstalledPackageReference, bool> filter = ListPackageHelper.TopLevelPackagesFilterForOutdated;
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference(
                    latestPackageVersionString: "2.0.0");

                // Act
                bool result = filter.Invoke(installedPackageReference);

                // Assert
                Assert.True(result);
            }
        }

        public class TransitivePackagesFilterForOutdated
        {
            [Fact]
            public void DoesNotFilterPackagesWithLatestMetadataNull()
            {
                // Arrange
                Func<InstalledPackageReference, bool> filter = ListPackageHelper.TransitivePackagesFilterForOutdated;
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference();
                installedPackageReference.LatestPackageMetadata = null;

                // Act
                bool result = filter.Invoke(installedPackageReference);

                // Assert
                Assert.True(result);
            }

            [Fact]
            public void DoesNotFilterPackagesWithNewerVersionAvailable()
            {
                // Arrange
                Func<InstalledPackageReference, bool> filter = ListPackageHelper.TransitivePackagesFilterForOutdated;
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference(
                    latestPackageVersionString: "2.0.0");

                // Act
                bool result = filter.Invoke(installedPackageReference);

                // Assert
                Assert.True(result);
            }

            [Theory]
            [InlineData(false, false)]
            [InlineData(false, true)]
            [InlineData(true, false)]
            [InlineData(true, true)]
            public void FiltersFrameworkPackagesCollectionWithOutdatedMetadata(
                bool includeTopLevelPositives,
                bool includeTransitivePositives)
            {
                // Arrange
                var packages = new FrameworkPackages("net40", "net40");
                var topLevelPackages =
                    new List<InstalledPackageReference>
                    {
                        ListPackageTestHelper.CreateInstalledPackageReference(resolvedPackageVersionString: "2.0.0",
                            latestPackageVersionString: "2.0.0")
                    };
                var transitivePackages =
                    new List<InstalledPackageReference>
                    {
                        ListPackageTestHelper.CreateInstalledPackageReference(resolvedPackageVersionString: "2.0.0",
                            latestPackageVersionString: "2.0.0")
                    };

                if (includeTopLevelPositives)
                {
                    topLevelPackages.Add(ListPackageTestHelper.CreateInstalledPackageReference(
                        resolvedPackageVersionString: "2.0.0", latestPackageVersionString: "3.0.0"));
                }

                if (includeTransitivePositives)
                {
                    transitivePackages.Add(ListPackageTestHelper.CreateInstalledPackageReference(
                        resolvedPackageVersionString: "2.0.0", latestPackageVersionString: "3.0.0"));
                }

                var output = new StringBuilder();
                var error = new StringBuilder();
                using TextWriter consoleOut = new StringWriter(output);
                using TextWriter consoleError = new StringWriter(error);

                packages.TopLevelPackages = topLevelPackages;
                packages.TransitivePackages = transitivePackages;
                var allPackages = new List<FrameworkPackages> { packages };
                var listPackageArgs = new ListPackageArgs(path: "", packageSources: new List<PackageSource>(),
                    frameworks: new List<string>(),
                    reportType: ReportType.Outdated,
                    renderer: new ListPackageConsoleRenderer(consoleOut, consoleError),
                    includeTransitive: true, prerelease: false, highestPatch: false, highestMinor: false,
                    auditSources: null,
                    logger: new Mock<ILogger>().Object,
                    packageSourceMapping: NoPackageSourceMapping,
                    explicitPackageSources: Array.Empty<PackageSource>(),
                    cancellationToken: CancellationToken.None);

                // Act
                var isFilteredSetNonEmpty = ListPackageCommandRunner.FilterPackages(allPackages, listPackageArgs);

                var a = new ListPackageCommandRunner(new MSBuildAPIUtility(NullLogger.Instance, virtualProjectBuilder: null));
                var b = a.UpdatePackagesWithSourceMetadata(allPackages, null, listPackageArgs);

                // Assert
                Assert.Equal(includeTopLevelPositives || includeTransitivePositives, isFilteredSetNonEmpty);
                Assert.Equal(includeTopLevelPositives ? 1 : 0, allPackages.First().TopLevelPackages.Count());
                Assert.Equal(includeTransitivePositives ? 1 : 0, allPackages.First().TransitivePackages.Count());
            }

            [Fact]
            public async Task UpdatePackages_WithNullSourceMetadata_Succeeds()
            {
                // Arrange
                ListPackageCommandRunner listPackageRunner = new ListPackageCommandRunner(new MSBuildAPIUtility(NullLogger.Instance, virtualProjectBuilder: null));
                FrameworkPackages packages = new FrameworkPackages("net40", "net40");
                List<InstalledPackageReference> topLevelPackages =
                    new List<InstalledPackageReference>
                    {
                        ListPackageTestHelper.CreateInstalledPackageReference(resolvedPackageVersionString: "2.0.0",
                            latestPackageVersionString: "3.0.0")
                    };
                List<InstalledPackageReference> transitivePackages =
                    new List<InstalledPackageReference>
                    {
                        ListPackageTestHelper.CreateInstalledPackageReference(resolvedPackageVersionString: "2.0.0",
                            latestPackageVersionString: "3.0.0")
                    };

                var output = new StringBuilder();
                var error = new StringBuilder();
                using TextWriter consoleOut = new StringWriter(output);
                using TextWriter consoleError = new StringWriter(error);

                packages.TopLevelPackages = topLevelPackages;
                packages.TransitivePackages = transitivePackages;
                List<FrameworkPackages> allPackages = new List<FrameworkPackages> { packages };
                ListPackageArgs listPackageArgs = new ListPackageArgs(path: "", packageSources: new List<PackageSource>(),
                    frameworks: new List<string>(),
                    reportType: ReportType.Outdated,
                    renderer: new ListPackageConsoleRenderer(consoleOut, consoleError),
                    includeTransitive: true, prerelease: false, highestPatch: true, highestMinor: true,
                    auditSources: null,
                    logger: new Mock<ILogger>().Object,
                    packageSourceMapping: NoPackageSourceMapping,
                    explicitPackageSources: Array.Empty<PackageSource>(),
                    cancellationToken: CancellationToken.None);

                // Act
                var emptyPackageSearchMetadata = new Dictionary<string, List<IPackageSearchMetadata>>(capacity: allPackages.Count);
                Exception exception = await Record.ExceptionAsync(async () => await listPackageRunner.UpdatePackagesWithSourceMetadata(allPackages, emptyPackageSearchMetadata, listPackageArgs));

                // Assert
                Assert.Null(exception);
            }
        }

        public class PackagesFilterForDeprecated
        {
            [Fact]
            public void FiltersPackagesWithoutDeprecationMetadata()
            {
                // Arrange
                Func<InstalledPackageReference, bool> filter = ListPackageHelper.PackagesFilterForDeprecated;
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference();

                // Act
                bool result = filter.Invoke(installedPackageReference);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public void DoesNotFilterPackagesWithDeprecationMetadata()
            {
                // Arrange
                Func<InstalledPackageReference, bool> filter = ListPackageHelper.PackagesFilterForDeprecated;
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference(isDeprecated: true);

                // Act
                bool result = filter.Invoke(installedPackageReference);

                // Assert
                Assert.True(result);
            }

            [Theory]
            [InlineData(false, false)]
            [InlineData(false, true)]
            [InlineData(true, false)]
            [InlineData(true, true)]
            public void FiltersFrameworkPackagesCollectionWithDeprecationMetadata(
                bool includeTopLevelPositives,
                bool includeTransitivePositives)
            {
                // Arrange
                var packages = new FrameworkPackages("net40", "net40");
                var topLevelPackages =
                    new List<InstalledPackageReference> { ListPackageTestHelper.CreateInstalledPackageReference() };
                var transitivePackages =
                    new List<InstalledPackageReference> { ListPackageTestHelper.CreateInstalledPackageReference() };
                if (includeTopLevelPositives)
                {
                    topLevelPackages.Add(ListPackageTestHelper.CreateInstalledPackageReference(isDeprecated: true));
                }
                if (includeTransitivePositives)
                {
                    transitivePackages.Add(ListPackageTestHelper.CreateInstalledPackageReference(isDeprecated: true));
                }

                var output = new StringBuilder();
                var error = new StringBuilder();
                using TextWriter consoleOut = new StringWriter(output);
                using TextWriter consoleError = new StringWriter(error);

                packages.TopLevelPackages = topLevelPackages;
                packages.TransitivePackages = transitivePackages;
                var allPackages = new List<FrameworkPackages> { packages };
                var listPackageArgs = new ListPackageArgs(path: "", packageSources: new List<PackageSource>(),
                    frameworks: new List<string>(),
                    reportType: ReportType.Deprecated,
                    renderer: new ListPackageConsoleRenderer(consoleOut, consoleError),
                    includeTransitive: true, prerelease: false, highestPatch: false, highestMinor: false, auditSources: null, logger: new Mock<ILogger>().Object,
                    packageSourceMapping: NoPackageSourceMapping,
                    explicitPackageSources: Array.Empty<PackageSource>(),
                    cancellationToken: CancellationToken.None);

                // Act
                var isFilteredSetNonEmpty = ListPackageCommandRunner.FilterPackages(allPackages, listPackageArgs);

                // Assert
                Assert.Equal(includeTopLevelPositives || includeTransitivePositives, isFilteredSetNonEmpty);
                Assert.Equal(includeTopLevelPositives ? 1 : 0, allPackages.First().TopLevelPackages.Count());
                Assert.Equal(includeTransitivePositives ? 1 : 0, allPackages.First().TransitivePackages.Count());
            }
        }

        public class PackagesFilterForVulnerable
        {
            [Fact]
            public void FiltersPackagesWithoutVulnerableMetadata()
            {
                // Arrange
                Func<InstalledPackageReference, bool> filter = ListPackageHelper.PackagesFilterForVulnerable;
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference();

                // Act
                bool result = filter.Invoke(installedPackageReference);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public void DoesNotFilterPackagesWithVulnerableMetadata()
            {
                // Arrange
                Func<InstalledPackageReference, bool> filter = ListPackageHelper.PackagesFilterForVulnerable;
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference(vulnerabilityCount: 1);

                // Act
                bool result = filter.Invoke(installedPackageReference);

                // Assert
                Assert.True(result);
            }

            [Theory]
            [InlineData(false, false)]
            [InlineData(false, true)]
            [InlineData(true, false)]
            [InlineData(true, true)]
            public void FiltersFrameworkPackagesCollectionWithVulnerableMetadata(
                bool includeTopLevelPositives,
                bool includeTransitivePositives)
            {
                // Arrange
                var packages = new FrameworkPackages("net40", "net40");
                var topLevelPackages =
                    new List<InstalledPackageReference> { ListPackageTestHelper.CreateInstalledPackageReference() };
                var transitivePackages =
                    new List<InstalledPackageReference> { ListPackageTestHelper.CreateInstalledPackageReference() };
                if (includeTopLevelPositives)
                {
                    topLevelPackages.Add(ListPackageTestHelper.CreateInstalledPackageReference(vulnerabilityCount: 1));
                }
                if (includeTransitivePositives)
                {
                    transitivePackages.Add(ListPackageTestHelper.CreateInstalledPackageReference(vulnerabilityCount: 1));
                }

                var output = new StringBuilder();
                var error = new StringBuilder();
                using TextWriter consoleOut = new StringWriter(output);
                using TextWriter consoleError = new StringWriter(error);

                packages.TopLevelPackages = topLevelPackages;
                packages.TransitivePackages = transitivePackages;
                var allPackages = new List<FrameworkPackages> { packages };
                var listPackageArgs = new ListPackageArgs(path: "", packageSources: new List<PackageSource>(),
                    frameworks: new List<string>(),
                    reportType: ReportType.Vulnerable,
                    renderer: new ListPackageConsoleRenderer(consoleOut, consoleError),
                    includeTransitive: true, prerelease: false, highestPatch: false, highestMinor: false, auditSources: null, logger: new Mock<ILogger>().Object,
                    packageSourceMapping: NoPackageSourceMapping,
                    explicitPackageSources: Array.Empty<PackageSource>(),
                    cancellationToken: CancellationToken.None);

                // Act
                var isFilteredSetNonEmpty = ListPackageCommandRunner.FilterPackages(allPackages, listPackageArgs);

                // Assert
                Assert.Equal(includeTopLevelPositives || includeTransitivePositives, isFilteredSetNonEmpty);
                Assert.Equal(includeTopLevelPositives ? 1 : 0, allPackages.First().TopLevelPackages.Count());
                Assert.Equal(includeTransitivePositives ? 1 : 0, allPackages.First().TransitivePackages.Count());
            }
        }

        [Fact]
        public async Task GetPackageMetadataAsync_WithEmptyPackageSources_DoesNotThrowDivideByZero()
        {
            // Arrange
            var packages = new FrameworkPackages("net40", "net40");
            var topLevelPackages = new List<InstalledPackageReference>
            {
                ListPackageTestHelper.CreateInstalledPackageReference("TestPackage")
            };
            packages.TopLevelPackages = topLevelPackages;
            var allPackages = new List<FrameworkPackages> { packages };

            var output = new StringBuilder();
            var error = new StringBuilder();
            using TextWriter consoleOut = new StringWriter(output);
            using TextWriter consoleError = new StringWriter(error);

            // Create ListPackageArgs with empty packageSources list to trigger the divide by zero scenario
            var listPackageArgs = new ListPackageArgs(
                path: "",
                packageSources: new List<PackageSource>(), // Empty package sources - this would cause divide by zero
                frameworks: new List<string>(),
                reportType: ReportType.Outdated, // This will trigger the code path that calls GetPackageMetadataAsync
                renderer: new ListPackageConsoleRenderer(consoleOut, consoleError),
                includeTransitive: false,
                prerelease: false,
                highestPatch: false,
                highestMinor: false,
                auditSources: null,
                logger: new Mock<ILogger>().Object,
                packageSourceMapping: NoPackageSourceMapping,
                explicitPackageSources: Array.Empty<PackageSource>(),
                cancellationToken: CancellationToken.None);

            var listPackageRunner = new ListPackageCommandRunner(new MSBuildAPIUtility(NullLogger.Instance, virtualProjectBuilder: null));

            // Act & Assert - Call the method directly since it's now internal
            Exception exception = await Record.ExceptionAsync(async () =>
            {
                await listPackageRunner.GetPackageMetadataAsync(allPackages, listPackageArgs);
            });

            Assert.Null(exception);
        }

        [Fact]
        public void GetPackageIds_UnionsTopLevelAndTransitivePackageIdsIgnoringCase()
        {
            List<FrameworkPackages> frameworks =
            [
                new("net8.0", "net8.0", [new("PackageA")], [new("PackageB")]),
                new("net472", "net472", [new("packagea")], []),
            ];

            List<string> result = ListPackageCommandRunner.GetPackageIds(frameworks, includeTransitive: true);

            IEnumerable<string> actual = result.OrderBy(id => id, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(new[] { "PackageA", "PackageB" }, actual);
        }

        [Fact]
        public void SponsorshipOrder_FollowsConfiguredSourceOrder()
        {
            List<PackageSource> packageSources = [new("https://first"), new("https://second")];
            var sponsorships = packageSources.AsEnumerable().Reverse()
                .Select(source => new PackageSponsorship(source.Source, []));
            var renderer = new ListPackageConsoleRenderer();
            ListPackageArgs args = ListPackageTestHelper.CreateSponsorArgs("", packageSources, renderer);
            var processor = new SponsorReportProcessor(args);

            List<PackageSponsorship> result = processor.OrderSponsorshipsByConfiguredSource(sponsorships);

            IEnumerable<string> expected = packageSources.Select(source => source.Source);
            IEnumerable<string> actual = result.Select(sponsorship => sponsorship.Source);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("", "mapped,unmapped")]
        [InlineData("Newtonsoft.Json", "mapped")]
        [InlineData("Some.Other.Package", "")]
        public void FilterSourcesByPackageSourceMapping_ReturnsOnlyMappedConfiguredSources(
            string mappedPattern,
            string expectedSourceNames)
        {
            var packageSources = new List<PackageSource>
            {
                new PackageSource("https://mapped.test/v3/index.json", name: "mapped"),
                new PackageSource("https://unmapped.test/v3/index.json", name: "unmapped"),
            };
            PackageSourceMapping sourceMapping = NoPackageSourceMapping;
            if (mappedPattern.Length != 0)
            {
                var patterns = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["mapped"] = new[] { mappedPattern },
                    ["unmapped"] = new[] { "Some.Other.Package" },
                };
                sourceMapping = new PackageSourceMapping(patterns);
            }

            var renderer = new ListPackageConsoleRenderer();
            ListPackageArgs listPackageArgs = ListPackageTestHelper.CreateSponsorArgs(
                "", packageSources, renderer, packageSourceMapping: sourceMapping);
            var processor = new SponsorReportProcessor(listPackageArgs);
            List<PackageSource> result = processor.FilterSourcesByPackageSourceMapping("Newtonsoft.Json");

            IEnumerable<string> values = result.Select(source => source.Name);
            string actual = string.Join(",", values);
            Assert.Equal(expectedSourceNames, actual);
        }

        [Fact]
        public void SponsorConfiguration_WithExplicitSource_UsesOnlyExplicitSource()
        {
            var explicitSource = new PackageSource("https://explicit.test/v3/index.json", "explicit");
            var configuredSource = new PackageSource("https://configured.test/v3/index.json", "configured");
            var renderer = new ListPackageConsoleRenderer();
            ListPackageArgs args = ListPackageTestHelper.CreateSponsorArgs(
                "",
                [explicitSource, configuredSource],
                renderer,
                explicitPackageSources: [explicitSource]);
            var processor = new SponsorReportProcessor(args);

            bool result = processor.Configure();

            Assert.True(result);
            Assert.Equal(new[] { explicitSource }, args.PackageSources);
            Assert.False(renderer.ShowSponsorshipSourceHint);
        }

        [Fact]
        public void SponsorConfiguration_WithPackageSourceMappingAndExplicitSource_ReturnsError()
        {
            var source = new PackageSource("https://source.test/v3/index.json", "source");
            var mapping = new PackageSourceMapping(
                new Dictionary<string, IReadOnlyList<string>> { ["source"] = ["*"] });
            var renderer = new ListPackageConsoleRenderer();
            ListPackageArgs args = ListPackageTestHelper.CreateSponsorArgs(
                "",
                [source],
                renderer,
                packageSourceMapping: mapping,
                explicitPackageSources: [source]);
            var processor = new SponsorReportProcessor(args);

            bool result = processor.Configure();

            Assert.False(result);
            ReportProblem problem = Assert.Single(renderer.GetProblems());
            Assert.Equal(ProblemType.Error, problem.ProblemType);
            Assert.Equal(Strings.ListPkg_SponsorPackageSourceMappingWithSource, problem.Text);
        }

        [Fact]
        public void SponsorConfiguration_WithPackageSourceMapping_LogsInformationAndHidesHint()
        {
            var source = new PackageSource("https://source.test/v3/index.json", "source");
            var mapping = new PackageSourceMapping(
                new Dictionary<string, IReadOnlyList<string>> { ["source"] = ["*"] });
            var renderer = new ListPackageConsoleRenderer();
            var logger = new Mock<ILogger>();
            ListPackageArgs args = ListPackageTestHelper.CreateSponsorArgs(
                "",
                [source],
                renderer,
                logger.Object,
                mapping);
            var processor = new SponsorReportProcessor(args);

            bool result = processor.Configure();

            Assert.True(result);
            Assert.False(renderer.ShowSponsorshipSourceHint);
            logger.Verify(
                value => value.LogInformation(Strings.ListPkg_SponsorPackageSourceMappingEnabled),
                Times.Once);
        }

        [Theory]
        [InlineData("https://source.test/v3/index.json", true)]
        [InlineData("https://api.nuget.org/v3/index.json", false)]
        public void SponsorConfiguration_ConfiguresNuGetOrgSourceHint(string sourceUrl, bool expected)
        {
            var renderer = new ListPackageConsoleRenderer();
            ListPackageArgs args = ListPackageTestHelper.CreateSponsorArgs(
                "",
                [new PackageSource(sourceUrl)],
                renderer);
            var processor = new SponsorReportProcessor(args);

            bool result = processor.Configure();

            Assert.True(result);
            Assert.Equal(expected, renderer.ShowSponsorshipSourceHint);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SponsorConfiguration_LogsMinimalProgressOnlyForConsole(bool consoleOutput)
        {
            IReportRenderer renderer = consoleOutput
                ? new ListPackageConsoleRenderer()
                : new ListPackageJsonRenderer(TextWriter.Null);
            var logger = new Mock<ILogger>();
            ListPackageArgs args = ListPackageTestHelper.CreateSponsorArgs(
                "",
                [new PackageSource("https://source.test/v3/index.json")],
                renderer,
                logger.Object);
            var processor = new SponsorReportProcessor(args);

            bool result = processor.Configure();

            Assert.True(result);
            logger.Verify(
                value => value.LogMinimal(Strings.ListPkg_SponsorCheckingSourcesAndProjects),
                consoleOutput ? Times.Once() : Times.Never());
        }

        [Theory]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, true)]
        public void WarnAboutIncompatibleOptions_ForSponsor_AddsWarning(
            bool prerelease,
            bool highestMinor,
            bool highestPatch)
        {
            var renderer = new ListPackageConsoleRenderer();
            var args = new ListPackageArgs(
                "",
                [],
                [],
                ReportType.Sponsor,
                renderer,
                includeTransitive: false,
                prerelease,
                highestPatch,
                highestMinor,
                auditSources: null,
                NullLogger.Instance,
                NoPackageSourceMapping,
                Array.Empty<PackageSource>(),
                CancellationToken.None);

            ListPackageCommandRunner.WarnAboutIncompatibleOptions(args);

            ReportProblem warning = Assert.Single(renderer.GetProblems());
            Assert.Equal(ProblemType.Warning, warning.ProblemType);
            Assert.Equal(Strings.ListPkg_VulnerableIgnoredOptions, warning.Text);
        }
    }
}
