// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class AssetTargetFallbackUtilityTests
    {
        [Fact]
        public void AssetTargetFallbackUtility_VerifyGetInvalidFallbackCombinationMessage()
        {
            var message = AssetTargetFallbackUtility.GetInvalidFallbackCombinationMessage("/tmp/project.csproj");

            message.Code.Should().Be(NuGetLogCode.NU1003);
            message.FilePath.Should().Be("/tmp/project.csproj");
            message.TargetGraphs.Should().BeEmpty("this applies to the entire project");
            message.Level.Should().Be(LogLevel.Error);
            message.Message.Should().Be("PackageTargetFallback is deprecated. Replace PackageTargetFallback references with AssetTargetFallback in the project environment.");
        }

        [Fact]
        public void AssetTargetFallbackUtility_HasInvalidFallbackCombinationVerifyTrue()
        {
            var tfis = new List<TargetFrameworkInformation>
            {
                new TargetFrameworkInformation()
                {
                    FrameworkName = NuGetFramework.Parse("netcoreapp2.0")
                }
            };
            tfis[0].Imports.Add(NuGetFramework.Parse("net461"));
            tfis[0].AssetTargetFallback.Add(NuGetFramework.Parse("net461"));

            var project = new PackageSpec(tfis);

            AssetTargetFallbackUtility.HasInvalidFallbackCombination(project).Should().BeTrue();
        }

        [Fact]
        public void AssetTargetFallbackUtility_HasInvalidFallbackCombinationVerifyTrueForMultipleFrameworks()
        {
            var tfis = new List<TargetFrameworkInformation>
            {
                new TargetFrameworkInformation()
                {
                    FrameworkName = NuGetFramework.Parse("netcoreapp2.0")
                },
                new TargetFrameworkInformation()
                {
                    FrameworkName = NuGetFramework.Parse("netcoreapp1.0")
                },
                new TargetFrameworkInformation()
                {
                    FrameworkName = NuGetFramework.Parse("netcoreapp1.1")
                }
            };
            tfis[0].Imports.Add(NuGetFramework.Parse("net461"));
            tfis[0].AssetTargetFallback.Add(NuGetFramework.Parse("net461"));

            var project = new PackageSpec(tfis);

            AssetTargetFallbackUtility.HasInvalidFallbackCombination(project).Should().BeTrue();
        }

        [Fact]
        public void AssetTargetFallbackUtility_HasInvalidFallbackCombinationVerifyFalseForMultipleFrameworks()
        {
            var tfis = new List<TargetFrameworkInformation>
            {
                new TargetFrameworkInformation()
                {
                    FrameworkName = NuGetFramework.Parse("netcoreapp2.0")
                },
                new TargetFrameworkInformation()
                {
                    FrameworkName = NuGetFramework.Parse("netcoreapp1.0")
                },
                new TargetFrameworkInformation()
                {
                    FrameworkName = NuGetFramework.Parse("netcoreapp1.1")
                }
            };

            // Add PTF to one framework, and ATF to another
            tfis[0].Imports.Add(NuGetFramework.Parse("net461"));
            tfis[1].AssetTargetFallback.Add(NuGetFramework.Parse("net461"));

            var project = new PackageSpec(tfis);

            AssetTargetFallbackUtility.HasInvalidFallbackCombination(project).Should().BeFalse();
        }

        [Fact]
        public void AssetTargetFallbackUtility_HasInvalidFallbackCombinationVerifyFalseWithPTFOnly()
        {
            var tfis = new List<TargetFrameworkInformation>
            {
                new TargetFrameworkInformation()
                {
                    FrameworkName = NuGetFramework.Parse("netcoreapp2.0")
                }
            };
            tfis[0].Imports.Add(NuGetFramework.Parse("net461"));

            var project = new PackageSpec(tfis);

            AssetTargetFallbackUtility.HasInvalidFallbackCombination(project).Should().BeFalse();
        }

        [Fact]
        public void AssetTargetFallbackUtility_HasInvalidFallbackCombinationVerifyFalseWithATFOnly()
        {
            var tfis = new List<TargetFrameworkInformation>
            {
                new TargetFrameworkInformation()
                {
                    FrameworkName = NuGetFramework.Parse("netcoreapp2.0")
                }
            };
            tfis[0].AssetTargetFallback.Add(NuGetFramework.Parse("net461"));

            var project = new PackageSpec(tfis);

            AssetTargetFallbackUtility.HasInvalidFallbackCombination(project).Should().BeFalse();
        }

        [Fact]
        public async Task AssetTargetFallbackUtility_ValidateFallbackFrameworkVerifyFalse()
        {
            var testLogger = new TestLogger();
            var tfis = new List<TargetFrameworkInformation>
            {
                new TargetFrameworkInformation()
                {
                    FrameworkName = NuGetFramework.Parse("netcoreapp2.0")
                }
            };
            tfis[0].AssetTargetFallback.Add(NuGetFramework.Parse("net461"));
            tfis[0].Imports.Add(NuGetFramework.Parse("net461"));

            var project = new PackageSpec(tfis);

            var success = await AssetTargetFallbackUtility.ValidateFallbackFrameworkAsync(project, testLogger);

            success.Should().BeFalse();
            testLogger.Errors.Should().Be(1);
        }

        [Fact]
        public async Task AssetTargetFallbackUtility_ValidateFallbackFrameworkVerifyTrue()
        {
            var testLogger = new TestLogger();
            var tfis = new List<TargetFrameworkInformation>
            {
                new TargetFrameworkInformation()
                {
                    FrameworkName = NuGetFramework.Parse("netcoreapp2.0")
                }
            };
            tfis[0].AssetTargetFallback.Add(NuGetFramework.Parse("net461"));

            var project = new PackageSpec(tfis);

            var success = await AssetTargetFallbackUtility.ValidateFallbackFrameworkAsync(project, testLogger);

            success.Should().BeTrue();
            testLogger.Messages.Should().BeEmpty();
        }
    }
}
