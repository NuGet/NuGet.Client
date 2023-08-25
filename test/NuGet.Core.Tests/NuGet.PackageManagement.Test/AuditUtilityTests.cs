// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using FluentAssertions;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Model;
using NuGet.Versioning;
using Xunit;

namespace NuGet.PackageManagement.Test
{
    public class AuditUtilityTests
    {
        [Fact]
        public void GetKnownVulnerability_WithPackageNotVulnerable_ReturnsNull()
        {
            List<IReadOnlyDictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>> knownVulnerabilities =
                new List<IReadOnlyDictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>>
                {
                    new Dictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>()
                    {
                        { "anotherPackageId",
                        new List<PackageVulnerabilityInfo> {
                            new PackageVulnerabilityInfo(
                                new Uri("https://contoso.com/random-vulnerability1"),
                                PackageVulnerabilitySeverity.Low,
                                VersionRange.Parse("[1.0.0, 2.0.0)")) }
                        }
                    }
                };

            AuditUtility.GetKnownVulnerabilities("packageId", new NuGetVersion(1, 0, 0), knownVulnerabilities).Should().BeNull();
        }

        [Fact]
        public void GetKnownVulnerability_WithPackageIdVulnerableButPackageVersionNotInRange_ReturnsNull()
        {
            List<IReadOnlyDictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>> knownVulnerabilities =
                new List<IReadOnlyDictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>>
                {
                    new Dictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>()
                    {
                        { "packageId",
                        new List<PackageVulnerabilityInfo> {
                            new PackageVulnerabilityInfo(
                                new Uri("https://contoso.com/random-vulnerability1"),
                                PackageVulnerabilitySeverity.Low,
                                VersionRange.Parse("[1.0.0, 2.0.0)")) }
                        }
                    }
                };

            AuditUtility.GetKnownVulnerabilities("packageId", new NuGetVersion(2, 0, 0), knownVulnerabilities).Should().BeNull();
        }

        [Fact]
        public void GetKnownVulnerability_WithPackageVulnerable_ReturnsOnlyAppropriateVulnerabilities()
        {
            List<IReadOnlyDictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>> knownVulnerabilities =
                new List<IReadOnlyDictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>>
                {
                    new Dictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>()
                    {
                        { "packageId",
                        new List<PackageVulnerabilityInfo> {
                                new PackageVulnerabilityInfo(
                                    new Uri("https://contoso.com/random-vulnerability1"),
                                    PackageVulnerabilitySeverity.Low,
                                    VersionRange.Parse("[1.0.0, 2.0.0)")),
                                new PackageVulnerabilityInfo(
                                    new Uri("https://contoso.com/random-vulnerability2"),
                                    PackageVulnerabilitySeverity.Moderate,
                                    VersionRange.Parse("[2.0.0, 2.0.0]")),
                                new PackageVulnerabilityInfo(
                                    new Uri("https://contoso.com/random-vulnerability3"),
                                    PackageVulnerabilitySeverity.High,
                                    VersionRange.Parse("[2.0.0, 3.0.0)"))
                            }
                        }
                    }
                };

            var vulnerabilities = AuditUtility.GetKnownVulnerabilities("packageId", new NuGetVersion(2, 0, 0), knownVulnerabilities);

            vulnerabilities.Should().HaveCount(2);
            var moderateVulnerability = vulnerabilities[0];
            moderateVulnerability.Severity.Should().Be(PackageVulnerabilitySeverity.Moderate);
            moderateVulnerability.Url.Should().Be(new Uri("https://contoso.com/random-vulnerability2"));
            moderateVulnerability.Versions.Should().Be(VersionRange.Parse("[2.0.0, 2.0.0]"));

            var highVulnerability = vulnerabilities[1];
            highVulnerability.Severity.Should().Be(PackageVulnerabilitySeverity.High);
            highVulnerability.Url.Should().Be(new Uri("https://contoso.com/random-vulnerability3"));
            highVulnerability.Versions.Should().Be(VersionRange.Parse("[2.0.0, 3.0.0)"));
        }

        [Fact]
        public void GetKnownVulnerability_WithPackageVulnerableFromMultipleSources_ReturnsAppropriateVulnerabilities()
        {
            List<IReadOnlyDictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>> knownVulnerabilities =
                new List<IReadOnlyDictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>>
                {
                    new Dictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>()
                    {
                        { "packageId",
                        new List<PackageVulnerabilityInfo> {
                                new PackageVulnerabilityInfo(
                                    new Uri("https://contoso.com/random-vulnerability1"),
                                    PackageVulnerabilitySeverity.Low,
                                    VersionRange.Parse("[1.0.0, 2.0.0)")),
                                new PackageVulnerabilityInfo(
                                    new Uri("https://contoso.com/random-vulnerability2"),
                                    PackageVulnerabilitySeverity.Moderate,
                                    VersionRange.Parse("[2.0.0, 2.0.0]")),
                                new PackageVulnerabilityInfo(
                                    new Uri("https://contoso.com/random-vulnerability3"),
                                    PackageVulnerabilitySeverity.High,
                                    VersionRange.Parse("[3.0.0, 4.0.0)"))
                            }
                        }
                    },
                    new Dictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>()
                    {
                        { "packageId",
                        new List<PackageVulnerabilityInfo> {
                                new PackageVulnerabilityInfo(
                                    new Uri("https://contoso.com/random-vulnerability4"),
                                    PackageVulnerabilitySeverity.Critical,
                                    VersionRange.Parse("[2.0.0, 2.5.0)"))
                            }
                        }
                    }
                };

            var vulnerabilities = AuditUtility.GetKnownVulnerabilities("packageId", new NuGetVersion(2, 0, 0), knownVulnerabilities);

            vulnerabilities.Should().HaveCount(2);
            var moderateVulnerability = vulnerabilities[0];
            moderateVulnerability.Severity.Should().Be(PackageVulnerabilitySeverity.Moderate);
            moderateVulnerability.Url.Should().Be(new Uri("https://contoso.com/random-vulnerability2"));
            moderateVulnerability.Versions.Should().Be(VersionRange.Parse("[2.0.0, 2.0.0]"));

            var highVulnerability = vulnerabilities[1];
            highVulnerability.Severity.Should().Be(PackageVulnerabilitySeverity.Critical);
            highVulnerability.Url.Should().Be(new Uri("https://contoso.com/random-vulnerability4"));
            highVulnerability.Versions.Should().Be(VersionRange.Parse("[2.0.0, 2.5.0)"));
        }

        [Fact]
        public void GetKnownVulnerability_WithPackageVulnerableFromMultipleSources_ReturnsDedupedVulnerabilities()
        {
            List<IReadOnlyDictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>> knownVulnerabilities =
                new List<IReadOnlyDictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>>
                {
                    new Dictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>()
                    {
                        { "packageId",
                        new List<PackageVulnerabilityInfo> {
                                new PackageVulnerabilityInfo(
                                    new Uri("https://contoso.com/random-vulnerability1"),
                                    PackageVulnerabilitySeverity.Low,
                                    VersionRange.Parse("[1.0.0, 2.0.0)")),
                                new PackageVulnerabilityInfo(
                                    new Uri("https://contoso.com/random-vulnerability2"),
                                    PackageVulnerabilitySeverity.Moderate,
                                    VersionRange.Parse("[2.0.0, 2.0.0]")),
                                new PackageVulnerabilityInfo(
                                    new Uri("https://contoso.com/random-vulnerability3"),
                                    PackageVulnerabilitySeverity.High,
                                    VersionRange.Parse("[3.0.0, 4.0.0)"))
                            }
                        }
                    },
                    new Dictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>()
                    {
                        { "packageId",
                        new List<PackageVulnerabilityInfo> {
                                new PackageVulnerabilityInfo(
                                    new Uri("https://contoso.com/random-vulnerability2"),
                                    PackageVulnerabilitySeverity.Moderate,
                                    VersionRange.Parse("[2.0.0, 2.0.0]"))
                            }
                        }
                    }
                };

            var vulnerabilities = AuditUtility.GetKnownVulnerabilities("packageId", new NuGetVersion(2, 0, 0), knownVulnerabilities);

            vulnerabilities.Should().HaveCount(1);
            var moderateVulnerability = vulnerabilities[0];
            moderateVulnerability.Severity.Should().Be(PackageVulnerabilitySeverity.Moderate);
            moderateVulnerability.Url.Should().Be(new Uri("https://contoso.com/random-vulnerability2"));
            moderateVulnerability.Versions.Should().Be(VersionRange.Parse("[2.0.0, 2.0.0]"));
        }

        [Theory]
        [InlineData(PackageVulnerabilitySeverity.Low, "low", NuGetLogCode.NU1901)]
        [InlineData(PackageVulnerabilitySeverity.Moderate, "moderate", NuGetLogCode.NU1902)]
        [InlineData(PackageVulnerabilitySeverity.High, "high", NuGetLogCode.NU1903)]
        [InlineData(PackageVulnerabilitySeverity.Critical, "critical", NuGetLogCode.NU1904)]
        [InlineData(PackageVulnerabilitySeverity.Unknown, "unknown", NuGetLogCode.NU1900)]
        public void GetSeverityLabelAndCode_ReturnCorrectLabelAndCode(PackageVulnerabilitySeverity severity, string expectedLabel, NuGetLogCode expectedCode)
        {
            AuditUtility.GetSeverityLabelAndCode(severity).Should().Be((expectedLabel, expectedCode));
        }
    }
}
