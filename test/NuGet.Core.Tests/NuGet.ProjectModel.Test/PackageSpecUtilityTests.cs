// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using FluentAssertions;
using NuGet.Frameworks;
using NuGet.Versioning;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class PackageSpecUtilityTests
    {
        [Theory]
        [InlineData("1.0-*")]
        [InlineData("1.0.0-*")]
        [InlineData("1.0.0-beta-*")]
        [InlineData("1.2.3.4-beta-*")]
        [InlineData("1.2.3.4-beta-a-*")]
        [InlineData("1.2.3.4-beta.*")]
        [InlineData("1.2.3.4-beta.1.*")]
        public void PackageSpecUtility_IsSnapshotVersion_True(string version)
        {
            Assert.True(PackageSpecUtility.IsSnapshotVersion(version));
        }

        [Theory]
        [InlineData("")]
        [InlineData("a")]
        [InlineData("1.0.0-beta.01.*")]
        [InlineData("1.0.0-beta-*.*")]
        [InlineData("1.2.3.4-beta*")]
        [InlineData("1.2.3.4-beta-**")]
        [InlineData("1.2.3.4-beta.**")]
        [InlineData("1.2.3.4-beta.1.*+beta")]
        [InlineData("1.2.3.4-beta.1+5.*")]
        public void PackageSpecUtility_IsSnapshotVersion_False(string version)
        {
            Assert.False(PackageSpecUtility.IsSnapshotVersion(version));
        }

        [Theory]
        [InlineData("1.0-*", "", "1.0.0")]
        [InlineData("2.0.0-beta-*", "", "2.0.0-beta")]
        [InlineData("2.0.0-beta.*", "", "2.0.0-beta")]
        [InlineData("2.0.0-beta.*", "2", "2.0.0-beta.2")]
        [InlineData("2.0.0-beta.5.*", "0", "2.0.0-beta.5.0")]
        [InlineData("2.0.0-beta.*", "final.release-label+git.hash", "2.0.0-beta.final.release-label+git.hash")]
        public void PackageSpecUtility_SpecifySnapshotVersion(string version, string snapshotValue, string expected)
        {
            // Arrange && Act
            var actual = PackageSpecUtility.SpecifySnapshot(version, snapshotValue);

            // Assert
            Assert.Equal(NuGetVersion.Parse(expected), actual);
        }

        [Fact]
        public void PackageSpecUtility_GetFallbackFrameworkWithNoFallbacksVerifyResult()
        {
            var project = NuGetFramework.Parse("netcoreapp2.0");
            var atf = new List<NuGetFramework>();
            var ptf = new List<NuGetFramework>();

            var result = PackageSpecUtility.GetFallbackFramework(project, ptf, atf);

            result.Should().Be(project, "no atf or ptf frameworks exist");
        }

        [Fact]
        public void PackageSpecUtility_GetFallbackFrameworkWithNullFallbacksVerifyResult()
        {
            var project = NuGetFramework.Parse("netcoreapp2.0");

            var result = PackageSpecUtility.GetFallbackFramework(project, packageTargetFallback: null, assetTargetFallback: null);

            result.Should().Be(project, "no atf or ptf frameworks exist");
        }

        [Fact]
        public void PackageSpecUtility_GetFallbackFrameworkWithPTFOnlyVerifyResult()
        {
            var project = NuGetFramework.Parse("netcoreapp2.0");
            var atf = new List<NuGetFramework>();
            var ptf = new List<NuGetFramework>()
            {
                NuGetFramework.Parse("net461")
            };

            var result = PackageSpecUtility.GetFallbackFramework(project, ptf, atf) as FallbackFramework;

            result.Fallback.ShouldBeEquivalentTo(ptf);
        }

        [Fact]
        public void PackageSpecUtility_GetFallbackFrameworkWithATFOnlyVerifyResult()
        {
            var project = NuGetFramework.Parse("netcoreapp2.0");
            var ptf = new List<NuGetFramework>();
            var atf = new List<NuGetFramework>()
            {
                NuGetFramework.Parse("net461")
            };

            var result = PackageSpecUtility.GetFallbackFramework(project, ptf, atf) as AssetTargetFallbackFramework;

            result.Fallback.ShouldBeEquivalentTo(atf);
        }

        [Fact]
        public void PackageSpecUtility_GetFallbackFrameworkWithATFAndPTFVerifyResult()
        {
            var project = NuGetFramework.Parse("netcoreapp2.0");
            var atf = new List<NuGetFramework>()
            {
                NuGetFramework.Parse("net461")
            };
            var ptf = new List<NuGetFramework>()
            {
                NuGetFramework.Parse("net461")
            };

            var result = PackageSpecUtility.GetFallbackFramework(project, ptf, atf);

            result.Should().Be(project, "both atf and ptf will be ignored");
        }

        [Fact]
        public void PackageSpecUtility_ApplyFallbackFrameworkWithBothFallbacksVerifyResult()
        {
            var frameworkInfo = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.Parse("netcoreapp2.0")
            };
            var atf = new List<NuGetFramework>()
            {
                NuGetFramework.Parse("net45")
            };
            var ptf = new List<NuGetFramework>()
            {
                NuGetFramework.Parse("net461")
            };

            PackageSpecUtility.ApplyFallbackFramework(frameworkInfo, ptf, atf);

            frameworkInfo.AssetTargetFallback.ShouldBeEquivalentTo(new[] { NuGetFramework.Parse("net45") });
            frameworkInfo.Imports.ShouldBeEquivalentTo(new[] { NuGetFramework.Parse("net461") });

            frameworkInfo.FrameworkName.Should().NotBeOfType(typeof(FallbackFramework));
            frameworkInfo.FrameworkName.Should().NotBeOfType(typeof(AssetTargetFallbackFramework));
        }

        [Fact]
        public void PackageSpecUtility_ApplyFallbackFrameworkWithNoFallbacksVerifyResult()
        {
            var frameworkInfo = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.Parse("netcoreapp2.0")
            };
            var atf = new List<NuGetFramework>();
            var ptf = new List<NuGetFramework>();

            PackageSpecUtility.ApplyFallbackFramework(frameworkInfo, ptf, atf);

            frameworkInfo.AssetTargetFallback.Should().BeEmpty();
            frameworkInfo.Imports.Should().BeEmpty();

            frameworkInfo.FrameworkName.Should().NotBeOfType(typeof(FallbackFramework));
            frameworkInfo.FrameworkName.Should().NotBeOfType(typeof(AssetTargetFallbackFramework));
        }

        [Fact]
        public void PackageSpecUtility_ApplyFallbackFrameworkWithPTFVerifyResult()
        {
            var frameworkInfo = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.Parse("netcoreapp2.0")
            };
            var atf = new List<NuGetFramework>();
            var ptf = new List<NuGetFramework>()
            {
                NuGetFramework.Parse("net461")
            };

            PackageSpecUtility.ApplyFallbackFramework(frameworkInfo, ptf, atf);

            frameworkInfo.AssetTargetFallback.Should().BeEmpty();
            frameworkInfo.Imports.ShouldBeEquivalentTo(new[] { NuGetFramework.Parse("net461") });

            frameworkInfo.FrameworkName.Should().BeOfType(typeof(FallbackFramework));
            frameworkInfo.FrameworkName.Should().NotBeOfType(typeof(AssetTargetFallbackFramework));
        }

        [Fact]
        public void PackageSpecUtility_ApplyFallbackFrameworkWithATFVerifyResult()
        {
            var frameworkInfo = new TargetFrameworkInformation()
            {
                FrameworkName = NuGetFramework.Parse("netcoreapp2.0")
            };
            var ptf = new List<NuGetFramework>();
            var atf = new List<NuGetFramework>()
            {
                NuGetFramework.Parse("net461")
            };

            PackageSpecUtility.ApplyFallbackFramework(frameworkInfo, ptf, atf);

            frameworkInfo.Imports.Should().BeEmpty();
            frameworkInfo.AssetTargetFallback.ShouldBeEquivalentTo(new[] { NuGetFramework.Parse("net461") });

            frameworkInfo.FrameworkName.Should().NotBeOfType(typeof(FallbackFramework));
            frameworkInfo.FrameworkName.Should().BeOfType(typeof(AssetTargetFallbackFramework));
        }
    }
}
