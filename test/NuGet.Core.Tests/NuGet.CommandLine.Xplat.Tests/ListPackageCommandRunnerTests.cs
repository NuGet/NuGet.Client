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
                    cancellationToken: CancellationToken.None,
                    packageSourceMapping: NoPackageSourceMapping);

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
                    cancellationToken: CancellationToken.None,
                    packageSourceMapping: NoPackageSourceMapping);

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
                    cancellationToken: CancellationToken.None,
                    packageSourceMapping: NoPackageSourceMapping);

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
                    cancellationToken: CancellationToken.None,
                    packageSourceMapping: NoPackageSourceMapping);

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
                cancellationToken: CancellationToken.None,
                packageSourceMapping: NoPackageSourceMapping);

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
            var frameworks = new List<FrameworkPackages>
            {
                new FrameworkPackages("net8.0", "net8.0",
                    new List<InstalledPackageReference> { ListPackageTestHelper.CreateInstalledPackageReference("Newtonsoft.Json") },
                    new List<InstalledPackageReference> { ListPackageTestHelper.CreateInstalledPackageReference("Serilog") }),
                new FrameworkPackages("net472", "net472",
                    new List<InstalledPackageReference> { ListPackageTestHelper.CreateInstalledPackageReference("newtonsoft.json") },
                    new List<InstalledPackageReference>()),
            };

            List<string> result = ListPackageCommandRunner.GetPackageIds(frameworks, includeTransitive: true);

            Assert.Equal(new[] { "Newtonsoft.Json", "Serilog" }, result.OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
        }

        [Fact]
        public void SponsorshipOrder_FollowsConfiguredSourceOrder()
        {
            var packageSources = new List<PackageSource>
            {
                new PackageSource("https://first.test/v3/index.json"),
                new PackageSource("https://second.test/v3/index.json"),
            };
            string[] urls = { "https://sponsor/a", "https://sponsor/b" };
            var sponsorships = new[]
            {
                new PackageSponsorship(packageSources[1].Source, urls),
                new PackageSponsorship("https://unconfigured.test/v3/index.json", urls),
                new PackageSponsorship(packageSources[0].Source, urls),
            };

            List<PackageSponsorship> result =
                ListPackageCommandRunner.OrderSponsorshipsByConfiguredSource(
                    sponsorships,
                    SponsorArgs(packageSources));

            Assert.Equal(
                new[]
                {
                    packageSources[0].Source,
                    packageSources[1].Source,
                    "https://unconfigured.test/v3/index.json",
                },
                result.Select(sponsorship => sponsorship.Source));
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
            PackageSourceMapping sourceMapping = mappedPattern.Length == 0
                ? CreatePackageSourceMapping()
                : CreatePackageSourceMapping(
                    ("mapped", mappedPattern),
                    ("unmapped", "Some.Other.Package"));

            List<PackageSource> result =
                ListPackageCommandRunner.FilterSourcesByPackageSourceMapping(
                    "Newtonsoft.Json",
                    SponsorArgs(packageSources, sourceMapping));

            string[] expected = expectedSourceNames.Length == 0
                ? Array.Empty<string>()
                : expectedSourceNames.Split(',');
            Assert.Equal(expected, result.Select(source => source.Name));
        }

        private static PackageSourceMapping CreatePackageSourceMapping(
            params (string sourceName, string pattern)[] mappings)
        {
            return new PackageSourceMapping(
                mappings.ToDictionary(
                    mapping => mapping.sourceName,
                    mapping => (IReadOnlyList<string>)new List<string> { mapping.pattern },
                    StringComparer.OrdinalIgnoreCase));
        }

        private static ListPackageArgs SponsorArgs(
            List<PackageSource> sources,
            PackageSourceMapping sourceMapping = null)
        {
            return new ListPackageArgs(
                path: "",
                packageSources: sources,
                frameworks: new List<string>(),
                reportType: ReportType.Sponsor,
                renderer: new ListPackageConsoleRenderer(),
                includeTransitive: false,
                prerelease: false,
                highestPatch: false,
                highestMinor: false,
                auditSources: null,
                logger: new Mock<ILogger>().Object,
                cancellationToken: CancellationToken.None,
                packageSourceMapping: sourceMapping ?? NoPackageSourceMapping);
        }

    }
}
