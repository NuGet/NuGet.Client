// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NuGet.Common;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.Test
{
    public class UnexpectedDependencyMessagesTests
    {
        [Fact]
        public void GivenAGraphWithMultipleIssuesForTheSamePackageVerifyBothMessagesLogged()
        {
            var parent1 = new LibraryIdentity("x", NuGetVersion.Parse("9.0.0"), LibraryType.Package);
            var parent2 = new LibraryIdentity("y", NuGetVersion.Parse("8.0.0"), LibraryType.Package);
            var child1 = new LibraryIdentity("b", NuGetVersion.Parse("2.0.0"), LibraryType.Package);
            var child2 = new LibraryIdentity("b", NuGetVersion.Parse("3.0.0"), LibraryType.Package);
            var dependency1 = new ResolvedDependencyKey(parent1, VersionRange.Parse("(, 5.0.0]"), child1);
            var dependency2 = new ResolvedDependencyKey(parent2, VersionRange.Parse("(1.0.0, 6.0.0]"), child2);
            var dependencySet = new HashSet<ResolvedDependencyKey>() { dependency1, dependency2 };
            var targetGraph = new Mock<IRestoreTargetGraph>();
            targetGraph.SetupGet(e => e.ResolvedDependencies).Returns(dependencySet);
            targetGraph.SetupGet(e => e.Name).Returns("net46/win10");
            var targetGraphs = new[] { targetGraph.Object };
            var ignore = new HashSet<string>();

            var logs = UnexpectedDependencyMessages.GetMissingLowerBounds(targetGraphs, ignore).ToList();

            logs.Select(e => e.Message).ShouldBeEquivalentTo(new[]
            {
                "x 9.0.0 does not provide an inclusive lower bound for dependency b (<= 5.0.0). An approximate best match of b 2.0.0 was resolved.",
                "y 8.0.0 does not provide an inclusive lower bound for dependency b (> 1.0.0 && <= 6.0.0). An approximate best match of b 3.0.0 was resolved."
            });
        }

        [Fact]
        public async Task GivenAProjectWithNoIssuesVerifyNoMessagesLogged()
        {
            var testLogger = new TestLogger();
            var range = VersionRange.Parse("[1.0.0, 3.0.0)");
            var tfi = GetTFI(NuGetFramework.Parse("net46"), new LibraryRange("x", range, LibraryDependencyTarget.Package));
            var project = new PackageSpec(tfi)
            {
                Name = "proj"
            };
            var flattened = new HashSet<GraphItem<RemoteResolveResult>>
            {
                new GraphItem<RemoteResolveResult>(new LibraryIdentity("X", NuGetVersion.Parse("1.0.0"), LibraryType.Package))
            };
            var targetGraph = new Mock<IRestoreTargetGraph>();
            targetGraph.SetupGet(e => e.Flattened).Returns(flattened);
            targetGraph.SetupGet(e => e.Name).Returns("net46/win10");
            targetGraph.SetupGet(e => e.Framework).Returns(NuGetFramework.Parse("net46"));
            var parent = new LibraryIdentity("z", NuGetVersion.Parse("9.0.0"), LibraryType.Package);
            var child = new LibraryIdentity("x", NuGetVersion.Parse("1.0.0"), LibraryType.Package);
            var dependency = new ResolvedDependencyKey(parent, VersionRange.Parse("[1.0.0, 3.0.0)"), child);
            var dependencySet = new HashSet<ResolvedDependencyKey>() { dependency };
            targetGraph.SetupGet(e => e.ResolvedDependencies).Returns(dependencySet);
            var targetGraphs = new[] { targetGraph.Object };
            var ignore = new HashSet<string>();

            await UnexpectedDependencyMessages.LogAsync(targetGraphs, project, testLogger);

            testLogger.Warnings.Should().Be(0);
        }

        [Fact]
        public async Task GivenAProjectWithMultipleWarningsForXVerifyOnlyFinalBumpedMessageIsShown()
        {
            var testLogger = new TestLogger();
            var range = VersionRange.Parse("[1.0.0, 3.0.0)");
            var tfi = GetTFI(NuGetFramework.Parse("net46"), new LibraryRange("x", range, LibraryDependencyTarget.Package));
            var project = new PackageSpec(tfi)
            {
                Name = "proj"
            };
            var flattened = new HashSet<GraphItem<RemoteResolveResult>>
            {
                new GraphItem<RemoteResolveResult>(new LibraryIdentity("X", NuGetVersion.Parse("2.0.0"), LibraryType.Package))
            };
            var targetGraph = new Mock<IRestoreTargetGraph>();
            targetGraph.SetupGet(e => e.Flattened).Returns(flattened);
            targetGraph.SetupGet(e => e.Name).Returns("net46/win10");
            targetGraph.SetupGet(e => e.Framework).Returns(NuGetFramework.Parse("net46"));
            var parent = new LibraryIdentity("z", NuGetVersion.Parse("9.0.0"), LibraryType.Package);
            var child = new LibraryIdentity("x", NuGetVersion.Parse("1.0.0"), LibraryType.Package);
            var dependency = new ResolvedDependencyKey(parent, VersionRange.Parse("[1.0.0, 3.0.0)"), child);
            var dependencySet = new HashSet<ResolvedDependencyKey>() { dependency };
            targetGraph.SetupGet(e => e.ResolvedDependencies).Returns(dependencySet);
            var targetGraphs = new[] { targetGraph.Object };
            var ignore = new HashSet<string>();

            await UnexpectedDependencyMessages.LogAsync(targetGraphs, project, testLogger);

            testLogger.LogMessages.Select(e => e.Code).Should().NotContain(NuGetLogCode.NU1604);
            testLogger.LogMessages.Select(e => e.Code).Should().Contain(NuGetLogCode.NU1601);
            testLogger.LogMessages.Select(e => e.Code).Should().NotContain(NuGetLogCode.NU1602);
            testLogger.LogMessages.Select(e => e.Code).Should().NotContain(NuGetLogCode.NU1603);
        }

        [Fact]
        public async Task GivenAProjectWithMultipleWarningsForXVerifyOnlyOneIsLogged()
        {
            var testLogger = new TestLogger();
            var range = VersionRange.Parse("[1.0.0, 3.0.0)");
            var tfi = GetTFI(NuGetFramework.Parse("net46"), new LibraryRange("x", range, LibraryDependencyTarget.Package));
            var project = new PackageSpec(tfi)
            {
                Name = "proj"
            };
            var flattened = new HashSet<GraphItem<RemoteResolveResult>>
            {
                new GraphItem<RemoteResolveResult>(new LibraryIdentity("X", NuGetVersion.Parse("2.0.0"), LibraryType.Package))
            };
            var targetGraph = new Mock<IRestoreTargetGraph>();
            targetGraph.SetupGet(e => e.Flattened).Returns(flattened);
            targetGraph.SetupGet(e => e.Name).Returns("net46/win10");
            targetGraph.SetupGet(e => e.Framework).Returns(NuGetFramework.Parse("net46"));
            var parent = new LibraryIdentity("z", NuGetVersion.Parse("9.0.0"), LibraryType.Package);
            var child = new LibraryIdentity("x", NuGetVersion.Parse("2.5.0"), LibraryType.Package);
            var dependency = new ResolvedDependencyKey(parent, VersionRange.Parse("(, 3.0.0)"), child);
            var dependencySet = new HashSet<ResolvedDependencyKey>() { dependency };
            targetGraph.SetupGet(e => e.ResolvedDependencies).Returns(dependencySet);
            var targetGraphs = new[] { targetGraph.Object };
            var ignore = new HashSet<string>();

            await UnexpectedDependencyMessages.LogAsync(targetGraphs, project, testLogger);

            testLogger.LogMessages.Select(e => e.Code).Should().NotContain(NuGetLogCode.NU1604);
            testLogger.LogMessages.Select(e => e.Code).Should().NotContain(NuGetLogCode.NU1601);
            testLogger.LogMessages.Select(e => e.Code).Should().Contain(NuGetLogCode.NU1602);
            testLogger.LogMessages.Select(e => e.Code).Should().NotContain(NuGetLogCode.NU1603);
        }

        [Fact]
        public async Task GivenAProjectWithMultipleWarningsForXVerifyOnlyTheFirstIsLogged()
        {
            var testLogger = new TestLogger();
            var range = VersionRange.Parse("(, 3.0.0)");
            var tfi = GetTFI(NuGetFramework.Parse("net46"), new LibraryRange("x", range, LibraryDependencyTarget.Package));
            var project = new PackageSpec(tfi)
            {
                Name = "proj"
            };
            var flattened = new HashSet<GraphItem<RemoteResolveResult>>
            {
                new GraphItem<RemoteResolveResult>(new LibraryIdentity("X", NuGetVersion.Parse("2.0.0"), LibraryType.Package))
            };
            var targetGraph = new Mock<IRestoreTargetGraph>();
            targetGraph.SetupGet(e => e.Flattened).Returns(flattened);
            targetGraph.SetupGet(e => e.Name).Returns("net46/win10");
            targetGraph.SetupGet(e => e.Framework).Returns(NuGetFramework.Parse("net46"));
            var parent = new LibraryIdentity("z", NuGetVersion.Parse("9.0.0"), LibraryType.Package);
            var child = new LibraryIdentity("x", NuGetVersion.Parse("2.5.0"), LibraryType.Package);
            var dependency = new ResolvedDependencyKey(parent, range, child);
            var dependencySet = new HashSet<ResolvedDependencyKey>() { dependency };
            targetGraph.SetupGet(e => e.ResolvedDependencies).Returns(dependencySet);
            var targetGraphs = new[] { targetGraph.Object };
            var ignore = new HashSet<string>();

            await UnexpectedDependencyMessages.LogAsync(targetGraphs, project, testLogger);

            testLogger.LogMessages.Select(e => e.Code).Should().Contain(NuGetLogCode.NU1604);
            testLogger.LogMessages.Select(e => e.Code).Should().NotContain(NuGetLogCode.NU1601);
            testLogger.LogMessages.Select(e => e.Code).Should().NotContain(NuGetLogCode.NU1602);
            testLogger.LogMessages.Select(e => e.Code).Should().NotContain(NuGetLogCode.NU1603);
        }

        [Fact]
        public void GivenAProjectWithABumpedNonInclusiveDependencyVerifyNoMessage()
        {
            var range = VersionRange.Parse("(1.0.0, 2.0.0]");
            var tfi = GetTFI(NuGetFramework.Parse("net46"), new LibraryRange("x", range, LibraryDependencyTarget.Reference));
            var project = new PackageSpec(tfi)
            {
                Name = "proj"
            };
            var flattened = new HashSet<GraphItem<RemoteResolveResult>>
            {
                new GraphItem<RemoteResolveResult>(new LibraryIdentity("x", NuGetVersion.Parse("2.0.0"), LibraryType.Reference))
            };
            var targetGraph = new Mock<IRestoreTargetGraph>();
            targetGraph.SetupGet(e => e.Flattened).Returns(flattened);
            targetGraph.SetupGet(e => e.Name).Returns("net46/win10");
            targetGraph.SetupGet(e => e.Framework).Returns(NuGetFramework.Parse("net46"));
            var targetGraphs = new[] { targetGraph.Object };
            var ignore = new HashSet<string>();

            UnexpectedDependencyMessages.GetBumpedUpDependencies(targetGraphs, project, ignore).Should().BeEmpty();
        }

        [Fact]
        public void GivenAProjectWithABumpedReferenceDependencyVerifyNoMessage()
        {
            var range = VersionRange.Parse("1.0.0");
            var tfi = GetTFI(NuGetFramework.Parse("net46"), new LibraryRange("x", range, LibraryDependencyTarget.Reference));
            var project = new PackageSpec(tfi)
            {
                Name = "proj"
            };
            var flattened = new HashSet<GraphItem<RemoteResolveResult>>
            {
                new GraphItem<RemoteResolveResult>(new LibraryIdentity("x", NuGetVersion.Parse("2.0.0"), LibraryType.Reference))
            };
            var targetGraph = new Mock<IRestoreTargetGraph>();
            targetGraph.SetupGet(e => e.Flattened).Returns(flattened);
            targetGraph.SetupGet(e => e.Name).Returns("net46/win10");
            targetGraph.SetupGet(e => e.Framework).Returns(NuGetFramework.Parse("net46"));
            var targetGraphs = new[] { targetGraph.Object };
            var ignore = new HashSet<string>();

            UnexpectedDependencyMessages.GetBumpedUpDependencies(targetGraphs, project, ignore).Should().BeEmpty();
        }

        [Fact]
        public void GivenAProjectWithABumpedDependencyThatIsIgnoredVerifyNoMessage()
        {
            var range = VersionRange.Parse("1.0.0");
            var tfi = GetTFI(NuGetFramework.Parse("net46"), new LibraryRange("x", range, LibraryDependencyTarget.Package));
            var project = new PackageSpec(tfi)
            {
                Name = "proj"
            };
            var flattened = new HashSet<GraphItem<RemoteResolveResult>>
            {
                new GraphItem<RemoteResolveResult>(new LibraryIdentity("x", NuGetVersion.Parse("2.0.0"), LibraryType.Package))
            };
            var targetGraph = new Mock<IRestoreTargetGraph>();
            targetGraph.SetupGet(e => e.Flattened).Returns(flattened);
            targetGraph.SetupGet(e => e.Name).Returns("net46/win10");
            targetGraph.SetupGet(e => e.Framework).Returns(NuGetFramework.Parse("net46"));
            var targetGraphs = new[] { targetGraph.Object };
            var ignore = new HashSet<string>() { "X" };

            UnexpectedDependencyMessages.GetBumpedUpDependencies(targetGraphs, project, ignore).Should().BeEmpty();
        }

        [Fact]
        public void GivenAProjectWithABumpedDependencyVerifyMessage()
        {
            var range = VersionRange.Parse("1.0.0");
            var tfi = GetTFI(NuGetFramework.Parse("net46"), new LibraryRange("x", range, LibraryDependencyTarget.Package));
            var project = new PackageSpec(tfi)
            {
                Name = "proj"
            };
            var flattened = new HashSet<GraphItem<RemoteResolveResult>>
            {
                new GraphItem<RemoteResolveResult>(new LibraryIdentity("X", NuGetVersion.Parse("2.0.0"), LibraryType.Package))
            };
            var targetGraph = new Mock<IRestoreTargetGraph>();
            targetGraph.SetupGet(e => e.Flattened).Returns(flattened);
            targetGraph.SetupGet(e => e.Name).Returns("net46/win10");
            targetGraph.SetupGet(e => e.Framework).Returns(NuGetFramework.Parse("net46"));
            var targetGraphs = new[] { targetGraph.Object };
            var ignore = new HashSet<string>();

            var log = UnexpectedDependencyMessages.GetBumpedUpDependencies(targetGraphs, project, ignore).Single();

            log.Code.Should().Be(NuGetLogCode.NU1601);
            log.TargetGraphs.ShouldBeEquivalentTo(new[] { "net46/win10" });
            log.Message.Should().Be("Dependency specified was x (>= 1.0.0) but ended up with X 2.0.0.");
        }

        [Fact]
        public void GivenAGraphIsMissingALowerBoundAndIdIsIgnoredVerifyWarningSkipped()
        {
            var range = VersionRange.Parse("(, 5.0.0]");
            var parent = new LibraryIdentity("a", NuGetVersion.Parse("9.0.0"), LibraryType.Package);
            var child = new LibraryIdentity("b", NuGetVersion.Parse("2.0.0"), LibraryType.Package);
            var dependency = new ResolvedDependencyKey(parent, range, child);
            var dependencySet = new HashSet<ResolvedDependencyKey>() { dependency };
            var targetGraph = new Mock<IRestoreTargetGraph>();
            targetGraph.SetupGet(e => e.ResolvedDependencies).Returns(dependencySet);
            targetGraph.SetupGet(e => e.Name).Returns("net46/win10");
            var targetGraphs = new[] { targetGraph.Object };
            var ignore = new HashSet<string>() { "B" };

            var logs = UnexpectedDependencyMessages.GetMissingLowerBounds(targetGraphs, ignore);

            logs.Should().BeEmpty();
        }

        [Fact]
        public void GivenAGraphIsMissingALowerBoundVerifyWarningIncludesGraphName()
        {
            var range = VersionRange.Parse("(, 5.0.0]");
            var parent = new LibraryIdentity("a", NuGetVersion.Parse("9.0.0"), LibraryType.Package);
            var child = new LibraryIdentity("b", NuGetVersion.Parse("2.0.0"), LibraryType.Package);
            var dependency = new ResolvedDependencyKey(parent, range, child);
            var dependencySet = new HashSet<ResolvedDependencyKey>() { dependency };
            var targetGraph = new Mock<IRestoreTargetGraph>();
            targetGraph.SetupGet(e => e.ResolvedDependencies).Returns(dependencySet);
            targetGraph.SetupGet(e => e.Name).Returns("net46/win10");
            var targetGraphs = new[] { targetGraph.Object };
            var ignore = new HashSet<string>();

            var log = UnexpectedDependencyMessages.GetMissingLowerBounds(targetGraphs, ignore).Single();

            log.TargetGraphs.ShouldBeEquivalentTo(new[] { "net46/win10" });
            log.Code.Should().Be(NuGetLogCode.NU1602);
        }

        [Fact]
        public void GivenAProjectWithMultipleDependencyBoundIssuesVerifyWarnings()
        {
            var tfi = new List<TargetFrameworkInformation>();
            tfi.AddRange(GetTFI(NuGetFramework.Parse("net46"), new LibraryRange("x", VersionRange.Parse("(, 2.0.0)"), LibraryDependencyTarget.Package)));
            tfi.AddRange(GetTFI(NuGetFramework.Parse("netstandard1.3"), new LibraryRange("y", VersionRange.Parse("(, 3.0.0)"), LibraryDependencyTarget.Package)));

            var project = new PackageSpec(tfi)
            {
                Name = "proj"
            };

            var logs = UnexpectedDependencyMessages.GetProjectDependenciesMissingLowerBounds(project);

            logs.Select(e => e.Code).ShouldAllBeEquivalentTo(NuGetLogCode.NU1604);
            logs.Select(e => e.Level).ShouldAllBeEquivalentTo(LogLevel.Warning);
            logs.Select(e => e.Message)
                .ShouldBeEquivalentTo(new[]
                {
                    "Project dependency x (< 2.0.0) does not contain an inclusive lower bound. Include a lower bound in the dependency version to ensure consistent restore results.",
                    "Project dependency y (< 3.0.0) does not contain an inclusive lower bound. Include a lower bound in the dependency version to ensure consistent restore results."
                });
        }

        [Fact]
        public void GivenAProjectWithMultipleFrameworksAndDifferentRangesVerifyDifferentWarningsPerPackage()
        {
            var tfi = new List<TargetFrameworkInformation>();
            tfi.AddRange(GetTFI(NuGetFramework.Parse("net46"), new LibraryRange("x", VersionRange.Parse("(, 2.0.0)"), LibraryDependencyTarget.Package)));
            tfi.AddRange(GetTFI(NuGetFramework.Parse("netstandard1.3"), new LibraryRange("x", VersionRange.Parse("(, 2.0.0]"), LibraryDependencyTarget.Package)));

            var project = new PackageSpec(tfi)
            {
                Name = "proj"
            };

            UnexpectedDependencyMessages.GetProjectDependenciesMissingLowerBounds(project).Count().Should().Be(2);
        }

        [Fact]
        public void GivenAProjectWithMultipleFrameworksAndSameDependenciesVerifyASingleWarningPerPackage()
        {
            var range = VersionRange.Parse("(, 2.0.0)");
            var tfi = new List<TargetFrameworkInformation>();
            tfi.AddRange(GetTFI(NuGetFramework.Parse("net46"), new LibraryRange("x", range, LibraryDependencyTarget.Package)));
            tfi.AddRange(GetTFI(NuGetFramework.Parse("netstandard1.3"), new LibraryRange("x", range, LibraryDependencyTarget.Package)));

            var project = new PackageSpec(tfi)
            {
                Name = "proj"
            };

            UnexpectedDependencyMessages.GetProjectDependenciesMissingLowerBounds(project).Count().Should().Be(1);
        }

        [Fact]
        public void GivenAProjectWithADependencyOnAPackageWithANullRanageVerifyWarningMessage()
        {
            var tfi = GetTFI(NuGetFramework.Parse("net46"), new LibraryRange("x", null, LibraryDependencyTarget.Package));
            var project = new PackageSpec(tfi)
            {
                Name = "proj"
            };

            var log = UnexpectedDependencyMessages.GetProjectDependenciesMissingLowerBounds(project).Single();

            log.Code.Should().Be(NuGetLogCode.NU1604);
            log.Message.Should().Be("Project dependency x does not contain an inclusive lower bound. Include a lower bound in the dependency version to ensure consistent restore results.");
        }

        [Fact]
        public void GivenAProjectWithADependencyOnAPackageWithNoLowerBoundVerifyWarningMessage()
        {
            var range = VersionRange.Parse("(, 2.0.0)");
            var tfi = GetTFI(NuGetFramework.Parse("net46"), new LibraryRange("x", range, LibraryDependencyTarget.Package));
            var project = new PackageSpec(tfi)
            {
                Name = "proj"
            };

            var log = UnexpectedDependencyMessages.GetProjectDependenciesMissingLowerBounds(project).Single();

            log.Code.Should().Be(NuGetLogCode.NU1604);
            log.Message.Should().Be("Project dependency x (< 2.0.0) does not contain an inclusive lower bound. Include a lower bound in the dependency version to ensure consistent restore results.");
        }

        [Fact]
        public void GivenAProjectWithADependencyOnAPackageWithANonInclusiveLowerBoundVerifyWarningMessage()
        {
            var range = VersionRange.Parse("(1.0.0, 2.0.0)");
            var tfi = GetTFI(NuGetFramework.Parse("net46"), new LibraryRange("x", range, LibraryDependencyTarget.Package));
            var project = new PackageSpec(tfi)
            {
                Name = "proj"
            };

            var log = UnexpectedDependencyMessages.GetProjectDependenciesMissingLowerBounds(project).Single();

            log.Code.Should().Be(NuGetLogCode.NU1604);
            log.Message.Should().Be("Project dependency x (> 1.0.0 && < 2.0.0) does not contain an inclusive lower bound. Include a lower bound in the dependency version to ensure consistent restore results.");
        }

        [Fact]
        public void GivenAProjectWithNullRangesForNonPackageDependenciesVersionNoWarnings()
        {
            var tfi = GetTFI(NuGetFramework.Parse("net46"), new LibraryRange("a", null, LibraryDependencyTarget.Project));
            var project = new PackageSpec(tfi)
            {
                Name = "proj",
                Dependencies = GetDependencyList(new LibraryRange("b", null, LibraryDependencyTarget.Reference))
            };

            UnexpectedDependencyMessages.GetProjectDependenciesMissingLowerBounds(project).Should().BeEmpty("non-project references should be ignored");
        }

        [Fact]
        public void GivenAProjectWithNonPackageDependenciesVersionNoWarnings()
        {
            var badRange = VersionRange.Parse("(, 2.0.0)");
            var tfi = GetTFI(NuGetFramework.Parse("net46"), new LibraryRange("a", badRange, LibraryDependencyTarget.Project));
            var project = new PackageSpec(tfi)
            {
                Name = "proj",
                Dependencies = GetDependencyList(new LibraryRange("b", badRange, LibraryDependencyTarget.Reference))
            };

            UnexpectedDependencyMessages.GetProjectDependenciesMissingLowerBounds(project).Should().BeEmpty("non-project references should be ignored");
        }

        [Fact]
        public void GivenAProjectWithCorrectDependenciesVerifyNoMissingLowerBoundWarnings()
        {
            var tfi = GetTFI(NuGetFramework.Parse("net46"), new LibraryRange("a", VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package));
            var project = new PackageSpec(tfi)
            {
                Name = "proj",
                Dependencies = GetDependencyList(new LibraryRange("b", VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package))
            };

            UnexpectedDependencyMessages.GetProjectDependenciesMissingLowerBounds(project).Should().BeEmpty("all dependencies are valid");
        }

        [Fact]
        public void GivenADependencyHasANonInclusiveLowerBoundVerifyMessage()
        {
            var range = VersionRange.Parse("(1.0.0, )");
            var parent = new LibraryIdentity("a", NuGetVersion.Parse("9.0.0"), LibraryType.Package);
            var child = new LibraryIdentity("b", NuGetVersion.Parse("2.0.0"), LibraryType.Package);
            var dependency = new ResolvedDependencyKey(parent, range, child);

            var log = UnexpectedDependencyMessages.GetMissingLowerBoundMessage(dependency);

            log.Code.Should().Be(NuGetLogCode.NU1602);
            log.Message.Should().Be("a 9.0.0 does not provide an inclusive lower bound for dependency b (> 1.0.0). An approximate best match of b 2.0.0 was resolved.");
        }

        [Fact]
        public void GivenADependencyHasNoLowerBoundVerifyMessage()
        {
            var range = VersionRange.Parse("(, 5.0.0]");
            var parent = new LibraryIdentity("a", NuGetVersion.Parse("9.0.0"), LibraryType.Package);
            var child = new LibraryIdentity("b", NuGetVersion.Parse("2.0.0"), LibraryType.Package);
            var dependency = new ResolvedDependencyKey(parent, range, child);

            var log = UnexpectedDependencyMessages.GetMissingLowerBoundMessage(dependency);

            log.Code.Should().Be(NuGetLogCode.NU1602);
            log.Message.Should().Be("a 9.0.0 does not provide an inclusive lower bound for dependency b (<= 5.0.0). An approximate best match of b 2.0.0 was resolved.");
        }

        [Fact]
        public void GivenAPackageDidNotResolveToTheMinimumVerifyMessage()
        {
            var range = VersionRange.Parse("1.0.0");
            var parent = new LibraryIdentity("a", NuGetVersion.Parse("9.0.0"), LibraryType.Package);
            var child = new LibraryIdentity("b", NuGetVersion.Parse("2.0.0"), LibraryType.Package);
            var dependency = new ResolvedDependencyKey(parent, range, child);

            var log = UnexpectedDependencyMessages.GetMissingLowerBoundMessage(dependency);

            log.Code.Should().Be(NuGetLogCode.NU1603);
            log.Message.Should().Be("a 9.0.0 depends on b (>= 1.0.0) but b 1.0.0 was not found. An approximate best match of b 2.0.0 was resolved.");
        }

        [Theory]
        [InlineData("1.0.0", "1.0.0")]
        [InlineData("1.0.0", "1.0.0+abc")]
        [InlineData("[1.0.0, ]", "1.0.0")]
        [InlineData("[1.0.0]", "1.0.0")]
        [InlineData("[1.0.0-beta, ]", "1.0.0-beta")]
        [InlineData("[1.0.0-beta, 2.0.0)", "1.0.0-beta")]
        public void GivenARangeVerifyItHasAnExactMatch(string rangeString, string childVersion)
        {
            var range = VersionRange.Parse(rangeString);
            var parent = new LibraryIdentity("a", NuGetVersion.Parse("9.0.0"), LibraryType.Package);
            var child = new LibraryIdentity("b", NuGetVersion.Parse(childVersion), LibraryType.Package);
            var dependency = new ResolvedDependencyKey(parent, range, child);

            UnexpectedDependencyMessages.DependencyRangeHasMissingExactMatch(dependency).Should().BeFalse();
        }

        [Theory]
        [InlineData("1.0.0", "2.0.0")]
        [InlineData("[1.0.0, ]", "1.0.0-beta")]
        [InlineData("[1.0.0-beta, ]", "1.0.1-beta")]
        [InlineData("(1.0.0-beta, 2.0.0)", "1.0.0-beta")]
        [InlineData("(,9.0.0)", "1.0.0")]
        [InlineData("[,9.0.0)", "1.0.0")]
        public void GivenARangeVerifyItDoesNotHaveAnExactMatch(string rangeString, string childVersion)
        {
            var range = VersionRange.Parse(rangeString);
            var parent = new LibraryIdentity("a", NuGetVersion.Parse("9.0.0"), LibraryType.Package);
            var child = new LibraryIdentity("b", NuGetVersion.Parse(childVersion), LibraryType.Package);
            var dependency = new ResolvedDependencyKey(parent, range, child);

            UnexpectedDependencyMessages.DependencyRangeHasMissingExactMatch(dependency).Should().BeTrue();
        }

        [Fact]
        public void GivenARangeVerifyProjectsCountAsExactMatches()
        {
            var range = VersionRange.Parse("( , 1.0.0]");
            var parent = new LibraryIdentity("a", NuGetVersion.Parse("9.0.0"), LibraryType.Project);
            var child = new LibraryIdentity("b", NuGetVersion.Parse("2.0.0"), LibraryType.Project);
            var dependency = new ResolvedDependencyKey(parent, range, child);

            UnexpectedDependencyMessages.DependencyRangeHasMissingExactMatch(dependency).Should().BeFalse("Project type should return false, regardless of the range.");
        }

        [Theory]
        [InlineData("(1.0.0, )")]
        [InlineData("(, 1.0.0]")]
        [InlineData("(1.0.0, 1.0.0)")]
        [InlineData("(1.0.0, 2.0.0)")]
        public void GivenARangeVerifyLowerBoundMissingIsTrue(string s)
        {
            UnexpectedDependencyMessages.HasMissingLowerBound(VersionRange.Parse(s)).Should().BeTrue();
        }

        [Theory]
        [InlineData("[1.0.0, )")]
        [InlineData("[1.0.0]")]
        [InlineData("[1.0.0, 2.0.0)")]
        [InlineData("[1.0.0-beta.*, 2.0.0)")]
        [InlineData("1.0.0-*")]
        public void GivenARangeVerifyLowerBoundMissingIsFalse(string s)
        {
            UnexpectedDependencyMessages.HasMissingLowerBound(VersionRange.Parse(s)).Should().BeFalse();
        }

        public void GivenANullRangeVerifyLowerBoundMissingIsTrue()
        {
            UnexpectedDependencyMessages.HasMissingLowerBound(range: null).Should().BeTrue();
        }

        public void GivenTheAllRangeVerifyLowerBoundMissingIsTrue()
        {
            UnexpectedDependencyMessages.HasMissingLowerBound(VersionRange.All).Should().BeTrue();
        }

        private static List<LibraryDependency> GetDependencyList(LibraryRange range)
        {
            return new List<LibraryDependency>() { new LibraryDependency() { LibraryRange = range } };
        }

        private static List<TargetFrameworkInformation> GetTFI(NuGetFramework framework, params LibraryRange[] dependencies)
        {
            return new List<TargetFrameworkInformation>()
            {
                new TargetFrameworkInformation()
                {
                    FrameworkName = framework,
                    Dependencies = dependencies.Select(e => new LibraryDependency(){ LibraryRange = e }).ToList()
                }
            };
        }
    }
}
