// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using FluentAssertions;
using NuGet.Frameworks;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class AssetTargetFallbackTests
    {
        [Fact]
        public void GivenAssetTargetFallbackTrueVerifyValuePersisted()
        {
            var spec = PackageSpecTestUtility.GetSpec(NuGetFramework.Parse("netcoreapp2.0"));
            spec.TargetFrameworks[0].AssetTargetFallback = true;

            var outSpec = spec.RoundTrip();
            outSpec.TargetFrameworks[0].AssetTargetFallback.Should().BeTrue();
        }

        [Fact]
        public void GivenAssetTargetFallbackFalseVerifyValuePersisted()
        {
            var spec = PackageSpecTestUtility.GetSpec(NuGetFramework.Parse("netcoreapp2.0"));
            spec.TargetFrameworks[0].AssetTargetFallback = false;

            var outSpec = spec.RoundTrip();
            outSpec.TargetFrameworks[0].AssetTargetFallback.Should().BeFalse();
        }

        [Fact]
        public void GivenImportsVerifyValuePersisted()
        {
            var net461 = NuGetFramework.Parse("net461");
            var spec = PackageSpecTestUtility.GetSpec(NuGetFramework.Parse("netcoreapp2.0"));
            spec.TargetFrameworks[0].AssetTargetFallback = true;
            spec.TargetFrameworks[0].Imports.Add(net461);

            var outSpec = spec.RoundTrip();
            outSpec.TargetFrameworks[0].Imports.ShouldBeEquivalentTo(new[] { net461 });
        }

        [Fact]
        public void GivenAssetTargetFallbackVerifyFrameworkIsAssetTargetFallbackFramework()
        {
            var net461 = NuGetFramework.Parse("net461");
            var projectFramework = NuGetFramework.Parse("netcoreapp2.0");
            var spec = PackageSpecTestUtility.GetSpec(projectFramework);
            spec.TargetFrameworks[0].AssetTargetFallback = true;
            spec.TargetFrameworks[0].Imports.Add(net461);

            var outSpec = spec.RoundTrip();

            var outFramework = spec.TargetFrameworks[0].FrameworkName;

            outFramework.Should().Be(new AssetTargetFallbackFramework(projectFramework, new[] { net461 }));
        }

        [Fact]
        public void GivenAssetTargetFallbackDiffersVerifyEquality()
        {
            var spec1 = PackageSpecTestUtility.GetSpec("netcoreapp2.0");
            spec1.TargetFrameworks[0].AssetTargetFallback = true;
            var spec2 = PackageSpecTestUtility.GetSpec("netcoreapp2.0");
            spec2.TargetFrameworks[0].AssetTargetFallback = false;

            spec1.Should().NotBe(spec2);
            spec1.GetHashCode().Should().NotBe(spec2.GetHashCode());
        }
    }
}
