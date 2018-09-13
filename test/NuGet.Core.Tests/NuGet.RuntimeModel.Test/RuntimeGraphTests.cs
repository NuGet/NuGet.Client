// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGet.Frameworks;
using NuGet.Versioning;
using Xunit;

namespace NuGet.RuntimeModel.Test
{
    public class RuntimeGraphTests
    {
        [Fact]
        public void MergingInEmptyGraphHasNoEffect()
        {
            var graph = new RuntimeGraph(new[]
                {
                    new RuntimeDescription("any"),
                    new RuntimeDescription("win8", new[] { "any" })
                });
            var newGraph = RuntimeGraph.Merge(graph, RuntimeGraph.Empty);
            Assert.Equal(new RuntimeGraph(new[]
                {
                    new RuntimeDescription("any"),
                    new RuntimeDescription("win8", new[] { "any" })
                }), newGraph);
        }

        [Fact]
        public void MergingAddsCompletelyNewRuntimes()
        {
            var leftGraph = new RuntimeGraph(new[]
                {
                    new RuntimeDescription("any"),
                    new RuntimeDescription("win8", new[] { "any", "win7" })
                });
            var rightGraph = new RuntimeGraph(new[]
                {
                    new RuntimeDescription("win7")
                });
            var graph = RuntimeGraph.Merge(leftGraph, rightGraph);
            Assert.Equal(new RuntimeGraph(new[]
                {
                    new RuntimeDescription("any"),
                    new RuntimeDescription("win8", new[] { "any", "win7" }),
                    new RuntimeDescription("win7")
                }), graph);
        }

        [Fact]
        public void MergingCombinesDependencySetsInRuntimesDefinedInBoth()
        {
            var leftGraph = new RuntimeGraph(new[]
                {
                    new RuntimeDescription("any"),
                    new RuntimeDescription("win8", new[] { "any", "win7" }, new[]
                        {
                            new RuntimeDependencySet("Foo"),
                        })
                });
            var rightGraph = new RuntimeGraph(new[]
                {
                    new RuntimeDescription("win8", new[]
                        {
                            new RuntimeDependencySet("Bar")
                        })
                });
            var graph = RuntimeGraph.Merge(leftGraph, rightGraph);
            Assert.Equal(new RuntimeGraph(new[]
                {
                    new RuntimeDescription("any"),
                    new RuntimeDescription("win8", new[] { "any", "win7" }, new[]
                        {
                            new RuntimeDependencySet("Foo"),
                            new RuntimeDependencySet("Bar")
                        }),
                }), graph);
        }

        [Fact]
        public void MergingReplacesDependencySetsDefinedInBoth()
        {
            var leftGraph = new RuntimeGraph(new[]
                {
                    new RuntimeDescription("any"),
                    new RuntimeDescription("win8", new[] { "any", "win7" }, new[]
                        {
                            new RuntimeDependencySet("Foo", new[]
                                {
                                    new RuntimePackageDependency("Foo.win8", new VersionRange(new NuGetVersion(1, 2, 3)))
                                }),
                        })
                });
            var rightGraph = new RuntimeGraph(new[]
                {
                    new RuntimeDescription("win8", new[]
                        {
                            new RuntimeDependencySet("Foo", new[]
                                {
                                    new RuntimePackageDependency("Foo.better.win8", new VersionRange(new NuGetVersion(4, 5, 6)))
                                }),
                        })
                });
            var graph = RuntimeGraph.Merge(leftGraph, rightGraph);
            Assert.Equal(new RuntimeGraph(new[]
                {
                    new RuntimeDescription("any"),
                    new RuntimeDescription("win8", new[] { "any", "win7" }, new[]
                        {
                            new RuntimeDependencySet("Foo", new[]
                                {
                                    new RuntimePackageDependency("Foo.better.win8", new VersionRange(new NuGetVersion(4, 5, 6)))
                                }),
                        }),
                }), graph);
        }

        [Theory]
        [InlineData("win7", "win7")]
        [InlineData("win8", "win8,win7")]
        [InlineData("win8-x86", "win8-x86,win8,win7-x86,win7")]
        public void ExpandShouldExpandRuntimeBasedOnGraph(string start, string expanded)
        {
            var graph = new RuntimeGraph(new[]
                {
                    new RuntimeDescription("win8-x86", new[] { "win8", "win7-x86" }),
                    new RuntimeDescription("win8", new[] { "win7" }),
                    new RuntimeDescription("win7-x86", new[] { "win7" }),
                    new RuntimeDescription("win7"),
                });
            Assert.Equal(
                expanded.Split(','),
                graph.ExpandRuntime(start).ToArray());
        }

        [Fact]
        public void MergeReplacesCompatibilityProfilesDefinedInRightWithValuesFromLeftIfLeftNonEmpty()
        {
            var leftGraph = new RuntimeGraph(new[]
            {
                new CompatibilityProfile("frob", new []
                {
                    new FrameworkRuntimePair(FrameworkConstants.CommonFrameworks.Dnx452, "frob")
                })
            });
            var rightGraph = new RuntimeGraph(new[]
            {
                new CompatibilityProfile("frob", new []
                {
                    new FrameworkRuntimePair(FrameworkConstants.CommonFrameworks.DnxCore50, "blob")
                })
            });
            var graph = RuntimeGraph.Merge(leftGraph, rightGraph);
            Assert.Equal(leftGraph, graph);
        }

        [Fact]
        public void MergeReplacesCompatibilityProfilesDefinedInRightIntoLeftIfLeftIsEmpty()
        {
            var leftGraph = new RuntimeGraph(new[]
            {
                new CompatibilityProfile("frob")
            });
            var rightGraph = new RuntimeGraph(new[]
            {
                new CompatibilityProfile("frob", new []
                {
                    new FrameworkRuntimePair(FrameworkConstants.CommonFrameworks.DnxCore50, "blob")
                })
            });
            var graph = RuntimeGraph.Merge(leftGraph, rightGraph);
            Assert.Equal(rightGraph, graph);
        }
    }
}
