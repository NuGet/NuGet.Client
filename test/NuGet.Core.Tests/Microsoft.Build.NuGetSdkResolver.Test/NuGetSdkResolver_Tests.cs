// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;
using SdkResolverContextBase = Microsoft.Build.Framework.SdkResolverContext;

namespace Microsoft.Build.NuGetSdkResolver.Test
{
    public class NuGetSdkResolverTests
    {
        [Fact]
        public void TryGetNuGetVersionForSdkGetsVersionFromGlobalJson()
        {
            var expectedVersions = new Dictionary<string, string>
            {
                {"foo", "5.11.77"},
                {"bar", "2.0.0"}
            };

            using (var testDirectory = TestDirectory.Create())
            {
                GlobalJsonReaderTests.WriteGlobalJson(testDirectory, expectedVersions);

                var context = new MockSdkResolverContext(Path.Combine(testDirectory.Path, "foo.proj"));

                VerifyTryGetNuGetVersionForSdk(
                    version: null,
                    expectedVersion: NuGetVersion.Parse(expectedVersions["foo"]),
                    context: context);
            }
        }

        [Fact]
        public void TryGetNuGetVersionForSdkGetsVersionFromState()
        {
            var context = new MockSdkResolverContext("foo.proj")
            {
                State = new Dictionary<string, string>
                {
                    {"foo", "1.2.3"}
                }
            };

            VerifyTryGetNuGetVersionForSdk(
                version: null,
                expectedVersion: NuGetVersion.Parse("1.2.3"),
                context: context);
        }

        [Fact]
        public void TryGetNuGetVersionForSdkInvalidVersion()
        {
            VerifyTryGetNuGetVersionForSdk(
                version: "abc",
                expectedVersion: null);
        }

        [Fact]
        public void TryGetNuGetVersionForSdkInvalidVersionInGlobalJson()
        {
            var context = new MockSdkResolverContext("foo.proj")
            {
                State = new Dictionary<string, string>
                {
                    {"foo", "abc"}
                }
            };

            VerifyTryGetNuGetVersionForSdk(
                version: "abc",
                expectedVersion: null,
                context: context);
        }

        [Fact]
        public void TryGetNuGetVersionForSdkSucceeds()
        {
            VerifyTryGetNuGetVersionForSdk(
                version: "3.2.1",
                expectedVersion: NuGetVersion.Parse("3.2.1"));
        }

        [Fact]
        public void TryGetNuGetVersionNoVersionSpecified()
        {
            var context = new MockSdkResolverContext("foo.proj")
            {
                State = new Dictionary<string, string>()
            };

            VerifyTryGetNuGetVersionForSdk(
                version: null,
                expectedVersion: null,
                context: context);
        }

        private void VerifyTryGetNuGetVersionForSdk(string version, NuGetVersion expectedVersion, SdkResolverContextBase context = null)
        {
            var result = NuGetSdkResolver.TryGetNuGetVersionForSdk("foo", version, context, out var parsedVersion);

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
