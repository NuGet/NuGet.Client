// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.Build.Framework;
using NuGet.Packaging;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace Microsoft.Build.NuGetSdkResolver.Test
{
    /// <summary>
    /// Represents tests for the <see cref="NuGetSdkResolver" /> class.
    /// </summary>
    public class NuGetSdkResolverTests
    {
        private const string PackageA = nameof(PackageA);

        private const string PackageB = nameof(PackageB);

        private const string ProjectName = "Test.csproj";

        private const string VersionOnePointZero = "1.0.0";

        /// <summary>
        /// Verifies that <see cref="NuGetSdkResolver.Resolve(SdkReference, SdkResolverContext, SdkResultFactory)" /> returns a failed <see cref="SdkResult" /> and logs an error when a package is not found on the configured feeds.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public void Resolve_WhenPackageDoesNotExists_ReturnsFailedSdkResultAndLogsError()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var sdkReference = new SdkReference(PackageA, VersionOnePointZero, minimumVersion: null);
                var sdkResolverContext = new MockSdkResolverContext(pathContext.WorkingDirectory);
                var sdkResultFactory = new MockSdkResultFactory();
                var sdkResolver = new NuGetSdkResolver();

                MockSdkResult result = sdkResolver.Resolve(sdkReference, sdkResolverContext, sdkResultFactory) as MockSdkResult;

                result.Should().NotBeNull();
                result.Success.Should().BeFalse();
                result.Path.Should().BeNull();
                result.Version.Should().BeNull();
                result.Errors.Should().BeEquivalentTo(new[] { $"Unable to find package {sdkReference.Name}. No packages exist with this id in source(s): source" });
                result.Warnings.Should().BeEmpty();
            }
        }

        /// <summary>
        /// Verifies that <see cref="NuGetSdkResolver.Resolve(SdkReference, SdkResolverContext, SdkResultFactory)" /> returns a valid <see cref="SdkResult" /> when a package is found on the feed.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public void Resolve_WhenPackageExists_ReturnsSucceededSdkResult()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var sdkReference = new SdkReference(PackageA, VersionOnePointZero, minimumVersion: null);
                var package = new SimpleTestPackageContext(sdkReference.Name, sdkReference.Version);
                package.AddFile("Sdk/Sdk.props", "<Project />");
                package.AddFile("Sdk/Sdk.targets", "<Project />");
                SimpleTestPackageUtility.CreateFolderFeedV3Async(pathContext.PackageSource, PackageSaveMode.Defaultv3, package).Wait();
                var sdkResolverContext = new MockSdkResolverContext(pathContext.WorkingDirectory);
                var sdkResultFactory = new MockSdkResultFactory();
                var sdkResolver = new NuGetSdkResolver();

                MockSdkResult result = sdkResolver.Resolve(sdkReference, sdkResolverContext, sdkResultFactory) as MockSdkResult;

                result.Should().NotBeNull();
                result.Success.Should().BeTrue();
                result.Path.Should().Be(Path.Combine(pathContext.UserPackagesFolder, sdkReference.Name.ToLowerInvariant(), sdkReference.Version, "Sdk"));
                result.Version.Should().Be(sdkReference.Version);
                result.Errors.Should().BeEmpty();
                result.Warnings.Should().BeEmpty();
            }
        }

        /// <summary>
        /// Verifies that <see cref="NuGetSdkResolver.TryGetNuGetVersionForSdk(string, string, SdkResolverContext, out object)" /> uses a global.json for versions if it exists.
        /// </summary>
        [Fact]
        public void TryGetNuGetVersionForSdk_WhenGlobalJsonExists_UsesVersionsFromGlobalJson()
        {
            var expectedVersions = new Dictionary<string, string>
            {
                [PackageA] = "5.11.77",
                [PackageB] = "2.0.0"
            };

            using (var testDirectory = TestDirectory.Create())
            {
                GlobalJsonReaderTests.WriteGlobalJson(testDirectory, expectedVersions);

                var sdkResolverContext = new MockSdkResolverContext(testDirectory);

                VerifyTryGetNuGetVersionForSdk(
                    version: null,
                    expectedVersion: NuGetVersion.Parse(expectedVersions[PackageA]),
                    sdkResolverContext);
            }
        }

        /// <summary>
        /// Verifies that <see cref="NuGetSdkResolver.TryGetNuGetVersionForSdk(string, string, SdkResolverContext, out object)" /> returns <c>null</c> when an invalid version is specified in global.json.
        /// </summary>
        [Fact]
        public void TryGetNuGetVersionForSdk_WhenInvalidVersionInGlobalJson_ReturnsNull()
        {
            var sdkResolverContext = new MockSdkResolverContext(ProjectName)
            {
                State = new Dictionary<string, string>
                {
                    [PackageA] = "InvalidVersion"
                }
            };

            VerifyTryGetNuGetVersionForSdk(
                version: null,
                expectedVersion: null,
                sdkResolverContext);
        }

        /// <summary>
        /// Verifies that <see cref="NuGetSdkResolver.TryGetNuGetVersionForSdk(string, string, SdkResolverContext, out object)" /> returns <c>null</c> when an invalid version is specified in a project.
        /// </summary>
        [Fact]
        public void TryGetNuGetVersionForSdk_WhenInvalidVersionSpecified_ReturnsNull()
        {
            var sdkResolverContext = new MockSdkResolverContext(ProjectName);

            VerifyTryGetNuGetVersionForSdk(
                version: "InvalidVersion",
                expectedVersion: null,
                sdkResolverContext);
        }

        /// <summary>
        /// Verifies that <see cref="NuGetSdkResolver.TryGetNuGetVersionForSdk(string, string, SdkResolverContext, out object)" /> returns a <see cref="NuGetVersion" /> when a project specifies a valid version but the project path is null.
        /// </summary>
        [Fact]
        public void TryGetNuGetVersionForSdk_WhenProjectPathIsNullAndVersionIsNotNull_ReturnsNuGetVersion()
        {
            var sdkResolverContext = new MockSdkResolverContext(projectPath: null);

            VerifyTryGetNuGetVersionForSdk(
                version: "1.0.0",
                expectedVersion: NuGetVersion.Parse("1.0.0"),
                sdkResolverContext);
        }

        /// <summary>
        /// Verifies that <see cref="NuGetSdkResolver.TryGetNuGetVersionForSdk(string, string, SdkResolverContext, out object)" /> returns <c>null</c> when the project path is <c>null</c>.
        /// </summary>
        [Fact]
        public void TryGetNuGetVersionForSdk_WhenProjectPathIsNullAndVersionIsNull_ReturnsNull()
        {
            var sdkResolverContext = new MockSdkResolverContext(projectPath: null);

            VerifyTryGetNuGetVersionForSdk(
                version: null,
                expectedVersion: null,
                sdkResolverContext);
        }

        /// <summary>
        /// Verifies that <see cref="NuGetSdkResolver.TryGetNuGetVersionForSdk(string, string, SdkResolverContext, out object)" /> returns <c>null</c> when the state of a previous call has no version specified.
        /// </summary>
        [Fact]
        public void TryGetNuGetVersionForSdk_WhenStateContainsNoVersion_ReturnsNull()
        {
            var sdkResolverContext = new MockSdkResolverContext(ProjectName)
            {
                State = new Dictionary<string, string>()
            };

            VerifyTryGetNuGetVersionForSdk(
                version: null,
                expectedVersion: null,
                sdkResolverContext);
        }

        private void VerifyTryGetNuGetVersionForSdk(string version, NuGetVersion expectedVersion, SdkResolverContext context)
        {
            var result = NuGetSdkResolver.TryGetNuGetVersionForSdk(PackageA, version, context, out var parsedVersion);

            if (expectedVersion != null)
            {
                result.Should().BeTrue();

                parsedVersion.Should().NotBeNull();

                parsedVersion.Should().Be(expectedVersion);
            }
            else
            {
                result.Should().BeFalse();

                parsedVersion.Should().BeNull();
            }
        }
    }
}
