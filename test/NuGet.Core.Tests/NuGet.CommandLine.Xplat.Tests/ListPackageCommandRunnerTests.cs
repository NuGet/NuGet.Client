// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Concurrent;
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
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class ListPackageCommandRunnerTests
    {
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
                cancellationToken: CancellationToken.None);

            var listPackageRunner = new ListPackageCommandRunner(new MSBuildAPIUtility(NullLogger.Instance, virtualProjectBuilder: null));

            // Act & Assert - Call the method directly since it's now internal
            Exception exception = await Record.ExceptionAsync(async () =>
            {
                await listPackageRunner.GetPackageMetadataAsync(allPackages, listPackageArgs);
            });

            Assert.Null(exception);
        }

        private static SourceRepository StubSourceRepository(
            PackageSource source,
            IReadOnlyList<string> sponsorshipUrls = null,
            bool hasRegistrationResource = true,
            Action onQueried = null,
            Task completeAfter = null)
        {
            RegistrationResourceV3 registrationResource = null;

            if (hasRegistrationResource)
            {
                var httpSource = new HttpSource(
                    source,
                    () => Task.FromResult<HttpHandlerResource>(
                        new TestHttpHandler(
                            new TestMessageHandler(new Dictionary<string, string>(), string.Empty))),
                    new Mock<IThrottle>().Object);

                var stub = new Mock<RegistrationResourceV3>(
                    httpSource,
                    new Uri("https://stub.test"))
                {
                    CallBase = false
                };
                stub
                    .Setup(r => r.GetPackageIdMetadataAsync(
                        It.IsAny<string>(),
                        It.IsAny<SourceCacheContext>(),
                        It.IsAny<ILogger>(),
                        It.IsAny<CancellationToken>()))
                    .Returns(async () =>
                    {
                        if (completeAfter != null)
                        {
                            await Task.WhenAny(completeAfter, Task.Delay(TimeSpan.FromSeconds(30)));
                        }

                        onQueried?.Invoke();
                        return sponsorshipUrls == null ? null : new PackageIdMetadata(sponsorshipUrls);
                    });

                registrationResource = stub.Object;
            }

            var repository = new Mock<SourceRepository>(source, Enumerable.Empty<INuGetResourceProvider>());
            repository
                .Setup(r => r.GetResourceAsync<RegistrationResourceV3>(It.IsAny<CancellationToken>()))
                .ReturnsAsync(registrationResource);

            return repository.Object;
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
                packageSourceMapping: sourceMapping);
        }

        private static List<FrameworkPackages> SponsorFrameworks(params string[] topLevelPackageIds)
        {
            return new List<FrameworkPackages>
            {
                new FrameworkPackages("net8.0", "net8.0",
                    topLevelPackageIds.Select(id => ListPackageTestHelper.CreateInstalledPackageReference(id)).ToList(),
                    new List<InstalledPackageReference>()),
            };
        }

        private static ListPackageCommandRunner SponsorRunner() =>
            new ListPackageCommandRunner(new MSBuildAPIUtility(NullLogger.Instance, virtualProjectBuilder: null));

        public class PackagesFilterForSponsorship
        {
            [Theory]
            [InlineData(0, false)]
            [InlineData(1, true)]
            public void IncludesOnlyPackagesWithAtLeastOneSponsorship(int sponsorshipCount, bool expected)
            {
                // Arrange
                var installedPackageReference = ListPackageTestHelper.CreateInstalledPackageReference();
                if (sponsorshipCount > 0)
                {
                    installedPackageReference.Sponsorships = new[] { new PackageSponsorship("https://source", new[] { "https://sponsor/a" }) };
                }

                // Act & Assert
                Assert.Equal(expected, ListPackageHelper.PackagesFilterForSponsorship.Invoke(installedPackageReference));
            }
        }

        [Fact]
        public async Task GetSponsorshipMetadataAsync_UnionsTopLevelAndTransitivePackageIdsIgnoringCase()
        {
            // Arrange
            var frameworks = new List<FrameworkPackages>
            {
                new FrameworkPackages("net8.0", "net8.0",
                    new List<InstalledPackageReference> { ListPackageTestHelper.CreateInstalledPackageReference("Newtonsoft.Json") },
                    new List<InstalledPackageReference> { ListPackageTestHelper.CreateInstalledPackageReference("Serilog") }),
                new FrameworkPackages("net472", "net472",
                    new List<InstalledPackageReference> { ListPackageTestHelper.CreateInstalledPackageReference("newtonsoft.json") },
                    new List<InstalledPackageReference>()),
            };

            // With no package sources no source is queried, so every package resolves to an empty sponsorship list.
            var listPackageArgs = new ListPackageArgs(
                path: "",
                packageSources: new List<PackageSource>(),
                frameworks: new List<string>(),
                reportType: ReportType.Sponsor,
                renderer: new ListPackageConsoleRenderer(),
                includeTransitive: false,
                prerelease: false,
                highestPatch: false,
                highestMinor: false,
                auditSources: null,
                logger: new Mock<ILogger>().Object,
                cancellationToken: CancellationToken.None);

            var listPackageRunner = new ListPackageCommandRunner(new MSBuildAPIUtility(NullLogger.Instance, virtualProjectBuilder: null));

            // Act
            Dictionary<string, List<PackageSponsorship>> result = await listPackageRunner.GetSponsorshipMetadataAsync(frameworks, listPackageArgs);

            // Assert
            Assert.Equal(new[] { "Newtonsoft.Json", "Serilog" }, result.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
            // UpdatePackagesWithSponsorshipMetadata looks up by package name, so casing differences must still resolve.
            Assert.True(result.ContainsKey("NEWTONSOFT.JSON"));
            Assert.All(result.Values, sponsorships => Assert.Empty(sponsorships));
        }

        [Fact]
        public void UpdatePackagesWithSponsorshipMetadata_GivesEveryReferenceOfAPackageTheSameListInstance()
        {
            // Arrange
            InstalledPackageReference topLevel = ListPackageTestHelper.CreateInstalledPackageReference("Newtonsoft.Json");
            InstalledPackageReference transitiveDifferentCase = ListPackageTestHelper.CreateInstalledPackageReference("newtonsoft.json");
            InstalledPackageReference unsponsored = ListPackageTestHelper.CreateInstalledPackageReference("Serilog");
            var frameworks = new List<FrameworkPackages>
            {
                new FrameworkPackages("net8.0", "net8.0",
                    new List<InstalledPackageReference> { topLevel },
                    new List<InstalledPackageReference> { unsponsored }),
                new FrameworkPackages("net472", "net472",
                    new List<InstalledPackageReference>(),
                    new List<InstalledPackageReference> { transitiveDifferentCase }),
            };
            var sponsorships = new List<PackageSponsorship> { new PackageSponsorship("https://source", new[] { "https://sponsor/a" }) };
            var sponsorshipsById = new Dictionary<string, List<PackageSponsorship>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Newtonsoft.Json"] = sponsorships,
            };

            // Act
            ListPackageCommandRunner.UpdatePackagesWithSponsorshipMetadata(frameworks, sponsorshipsById);

            // Assert
            // SponsorReportAggregator keeps only the first instance of a package id, which is only safe
            // because every instance is handed the same list.
            Assert.Same(sponsorships, topLevel.Sponsorships);
            Assert.Same(sponsorships, transitiveDifferentCase.Sponsorships);
            Assert.Empty(unsponsored.Sponsorships);
        }

        public class SponsorshipSources
        {
            private const string SponsoringSource = "https://sponsoring.test/v3/index.json";
            private const string EmptySource = "https://empty.test/v3/index.json";
            private const string NotFoundSource = "https://notfound.test/v3/index.json";
            private const string SourceWithoutRegistrationResource = "https://local.test/v3/index.json";

            private static readonly string[] SponsorshipUrls = { "https://sponsor/a", "https://sponsor/b" };

            [Fact]
            public async Task OnlySourcesThatReturnUrls_AppearOnThePackage()
            {
                // Arrange
                var sources = new List<PackageSource>
                {
                    new PackageSource(SponsoringSource),
                    new PackageSource(EmptySource),
                    new PackageSource(NotFoundSource),
                    new PackageSource(SourceWithoutRegistrationResource),
                };
                ListPackageCommandRunner runner = SponsorRunner();
                runner._sourceRepositoryCache[sources[0]] = StubSourceRepository(sources[0], SponsorshipUrls);
                runner._sourceRepositoryCache[sources[1]] = StubSourceRepository(sources[1], Array.Empty<string>());
                runner._sourceRepositoryCache[sources[2]] = StubSourceRepository(sources[2], sponsorshipUrls: null);
                runner._sourceRepositoryCache[sources[3]] = StubSourceRepository(
                    sources[3],
                    hasRegistrationResource: false);

                // Act
                Dictionary<string, List<PackageSponsorship>> sponsorshipsById =
                    await runner.GetSponsorshipMetadataAsync(
                        SponsorFrameworks("Newtonsoft.Json"),
                        SponsorArgs(sources));

                // Assert
                PackageSponsorship sponsorship = Assert.Single(sponsorshipsById["Newtonsoft.Json"]);
                Assert.Equal(SponsoringSource, sponsorship.Source);
                Assert.Equal(SponsorshipUrls, sponsorship.Urls);
            }

            [Fact]
            public async Task SponsorshipOrder_FollowsConfiguredSourceOrder_WhenSourcesRespondOutOfOrder()
            {
                // Arrange
                var lastSourceAnswered = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var actualCompletionOrder = new ConcurrentQueue<string>();
                var sources = new List<PackageSource>
                {
                    new PackageSource("https://gated.test/v3/index.json"),
                    new PackageSource("https://middle.test/v3/index.json"),
                    new PackageSource("https://first-to-answer.test/v3/index.json"),
                };
                ListPackageCommandRunner runner = SponsorRunner();

                for (int i = 0; i < sources.Count; i++)
                {
                    PackageSource source = sources[i];
                    bool isLast = i == sources.Count - 1;

                    runner._sourceRepositoryCache[source] = StubSourceRepository(
                        source,
                        SponsorshipUrls,
                        onQueried: () =>
                        {
                            actualCompletionOrder.Enqueue(source.Source);
                            if (isLast)
                            {
                                lastSourceAnswered.TrySetResult(true);
                            }
                        },
                        completeAfter: i == 0 ? lastSourceAnswered.Task : null);
                }

                // Act
                Dictionary<string, List<PackageSponsorship>> sponsorshipsById =
                    await runner.GetSponsorshipMetadataAsync(
                        SponsorFrameworks("Newtonsoft.Json"),
                        SponsorArgs(sources));

                // Assert
                Assert.Equal(sources[0].Source, actualCompletionOrder.Last());

                Assert.Equal(
                    sources.Select(s => s.Source),
                    sponsorshipsById["Newtonsoft.Json"].Select(s => s.Source));
            }
        }

        public class PackageSourceMappingFilter
        {
            private const string MappedSource = "https://mapped.test/v3/index.json";
            private const string UnmappedSource = "https://unmapped.test/v3/index.json";
            private const string PackageId = "Newtonsoft.Json";

            private static PackageSourceMapping CreatePackageSourceMapping(
                params (string sourceName, string pattern)[] mappings)
            {
                return new PackageSourceMapping(
                    mappings.ToDictionary(
                        m => m.sourceName,
                        m => (IReadOnlyList<string>)new List<string> { m.pattern },
                        StringComparer.OrdinalIgnoreCase));
            }

            [Theory]
            [InlineData("", "mapped,unmapped")]
            [InlineData(PackageId, "mapped")]
            [InlineData("Some.Other.Package", "")]
            public async Task GetSponsorshipMetadataAsync_FiltersSourcesUsingPackageSourceMapping(
                string mappedPattern,
                string expectedSourceNames)
            {
                // Arrange
                var queriedSourceNames = new ConcurrentBag<string>();
                var sources = new List<PackageSource>
                {
                    new PackageSource(MappedSource, name: "mapped"),
                    new PackageSource(UnmappedSource, name: "unmapped"),
                };
                PackageSourceMapping sourceMapping = mappedPattern.Length == 0
                    ? CreatePackageSourceMapping()
                    : CreatePackageSourceMapping(
                        ("mapped", mappedPattern),
                        ("unmapped", "Some.Other.Package"));
                ListPackageCommandRunner runner = SponsorRunner();

                foreach (PackageSource source in sources)
                {
                    runner._sourceRepositoryCache[source] = StubSourceRepository(
                        source,
                        new[] { "https://sponsor/a" },
                        onQueried: () => queriedSourceNames.Add(source.Name));
                }

                // Act
                Dictionary<string, List<PackageSponsorship>> sponsorshipsById =
                    await runner.GetSponsorshipMetadataAsync(
                    SponsorFrameworks(PackageId),
                    SponsorArgs(sources, sourceMapping));

                // Assert
                string[] expected = expectedSourceNames.Length == 0
                        ? Array.Empty<string>()
                        : expectedSourceNames.Split(',');
                Assert.Equal(
                    expected,
                    queriedSourceNames.OrderBy(name => name, StringComparer.Ordinal));
                Assert.Equal(expected.Length, sponsorshipsById[PackageId].Count);
            }
        }
    }
}
