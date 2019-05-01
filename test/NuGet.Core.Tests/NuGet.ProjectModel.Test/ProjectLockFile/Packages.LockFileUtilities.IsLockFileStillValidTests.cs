using System.Collections.Generic;
using System.Linq;
using NuGet.Commands;
using NuGet.ProjectModel.Test.Builders;
using Xunit;
using static NuGet.Frameworks.FrameworkConstants;
using PackagesLockFileBuilder = NuGet.ProjectModel.Test.Builders.PackagesLockFileBuilder;

namespace NuGet.ProjectModel.Test.ProjectLockFile
{
    public partial class LockFileUtilities
    {
        public class IsLockFileStillValidTests
        {
            [Fact]
            public void DifferentVersionsAreNotEqual()
            {
                var x = new PackagesLockFileBuilder().Build();
                var y = new PackagesLockFileBuilder()
                    .WithVersion(2)
                    .Build();

                var actual = PackagesLockFileUtilities.IsLockFileStillValid(x, y);
                Assert.False(actual.lockFileStillValid);

                actual = PackagesLockFileUtilities.IsLockFileStillValid(y, x);
                Assert.False(actual.lockFileStillValid);
            }

            [Fact]
            public void DifferentTargetCountsAreNotEqual()
            {
                var x = new PackagesLockFileBuilder()
                    .WithTarget(target => target.WithFramework(CommonFrameworks.NetStandard20))
                    .WithTarget(target => target.WithFramework(CommonFrameworks.NetCoreApp22))
                    .Build();
                var y = new PackagesLockFileBuilder()
                    .WithTarget(target => target.WithFramework(CommonFrameworks.NetStandard20))
                    .Build();

                var actual = PackagesLockFileUtilities.IsLockFileStillValid(x, y);
                Assert.False(actual.lockFileStillValid);

                actual = PackagesLockFileUtilities.IsLockFileStillValid(y, x);
                Assert.False(actual.lockFileStillValid);
            }

            [Fact]
            public void DifferentTargetsksAreNotEqual()
            {
                var x = new PackagesLockFileBuilder()
                    .WithTarget(target => target.WithFramework(CommonFrameworks.NetStandard20))
                    .Build();
                var y = new PackagesLockFileBuilder()
                    .WithTarget(target => target.WithFramework(CommonFrameworks.NetCoreApp22))
                    .Build();

                var actual = PackagesLockFileUtilities.IsLockFileStillValid(x, y);
                Assert.False(actual.lockFileStillValid);

                actual = PackagesLockFileUtilities.IsLockFileStillValid(y, x);
                Assert.False(actual.lockFileStillValid);
            }

            [Fact]
            public void DifferentDependencyCountsAreNotEqual()
            {
                var x = new PackagesLockFileBuilder()
                    .WithTarget(target => target
                        .WithFramework(CommonFrameworks.NetStandard20)
                        .WithDependency(dep => dep.WithId("PackageA"))
                    )
                    .Build();
                var y = new PackagesLockFileBuilder()
                    .WithTarget(target => target
                        .WithFramework(CommonFrameworks.NetStandard20)
                        .WithDependency(dep => dep.WithId("PackageA"))
                        .WithDependency(dep => dep.WithId("PackageB"))
                    )
                    .Build();

                var actual = PackagesLockFileUtilities.IsLockFileStillValid(x, y);
                Assert.False(actual.lockFileStillValid);

                actual = PackagesLockFileUtilities.IsLockFileStillValid(y, x);
                Assert.False(actual.lockFileStillValid);
            }

            [Fact]
            public void DifferentDependencyAreNotEqual()
            {
                var x = new PackagesLockFileBuilder()
                    .WithTarget(target => target
                        .WithFramework(CommonFrameworks.NetStandard20)
                        .WithDependency(dep => dep.WithId("PackageA"))
                    )
                    .Build();
                var y = new PackagesLockFileBuilder()
                    .WithTarget(target => target
                        .WithFramework(CommonFrameworks.NetStandard20)
                        .WithDependency(dep => dep.WithId("PackageB"))
                    )
                    .Build();

                var actual = PackagesLockFileUtilities.IsLockFileStillValid(x, y);
                Assert.False(actual.lockFileStillValid);

                actual = PackagesLockFileUtilities.IsLockFileStillValid(y, x);
                Assert.False(actual.lockFileStillValid);
            }

            [Fact]
            public void MatchesDependencies()
            {
                var x = new PackagesLockFileBuilder()
                    .WithTarget(target => target
                        .WithFramework(CommonFrameworks.NetStandard20)
                        .WithDependency(dep => dep
                            .WithId("PackageA")
                            .WithContentHash("ABC"))
                        .WithDependency(dep => dep
                            .WithId("PackageB")
                            .WithContentHash("123"))
                    )
                    .Build();
                var y = new PackagesLockFileBuilder()
                    .WithTarget(target => target
                        .WithFramework(CommonFrameworks.NetStandard20)
                        .WithDependency(dep => dep
                            .WithId("PackageA")
                            .WithContentHash("XYZ"))
                        .WithDependency(dep => dep
                            .WithId("PackageB")
                            .WithContentHash("890"))
                    )
                    .Build();

                var actual = PackagesLockFileUtilities.IsLockFileStillValid(x, y);
                Assert.True(actual.lockFileStillValid);
                Assert.NotNull(actual.matchedDependencies);
                Assert.Equal(2, actual.matchedDependencies.Count);
                var depKvp = actual.matchedDependencies.Single(d => d.Key.Id == "PackageA");
                Assert.Equal("ABC", depKvp.Key.ContentHash);
                Assert.Equal("XYZ", depKvp.Value.ContentHash);
                depKvp = actual.matchedDependencies.Single(d => d.Key.Id == "PackageB");
                Assert.Equal("123", depKvp.Key.ContentHash);
                Assert.Equal("890", depKvp.Value.ContentHash);

                actual = PackagesLockFileUtilities.IsLockFileStillValid(y, x);
                Assert.True(actual.lockFileStillValid);
                Assert.NotNull(actual.matchedDependencies);
                Assert.Equal(2, actual.matchedDependencies.Count);
                depKvp = actual.matchedDependencies.Single(d => d.Key.Id == "PackageA");
                Assert.Equal("ABC", depKvp.Value.ContentHash);
                Assert.Equal("XYZ", depKvp.Key.ContentHash);
                depKvp = actual.matchedDependencies.Single(d => d.Key.Id == "PackageB");
                Assert.Equal("123", depKvp.Value.ContentHash);
                Assert.Equal("890", depKvp.Key.ContentHash);
            }
        }
    }
}
