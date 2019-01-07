// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    // Tests the Powershell autocomplete resource for V2 and v3 sources.  
    public class PowershellAutoCompleteResourceTests
    {
        private static Dictionary<string, string> ResponsesDict;
        public PowershellAutoCompleteResourceTests()
        {
            ResponsesDict = new Dictionary<string, string>();
            ResponsesDict.Add(
                "http://source.test/v3/index.json",
                PowershellJsonData.IndexJson);
            ResponsesDict.Add(
                "https://nuget.org/api/v2/",
                string.Empty);
            ResponsesDict.Add(
                "https://api-v3search-0.nuget.org/autocomplete?q=elm&prerelease=true&semVerLevel=2.0.0",
                PowershellJsonData.AutoCompleteV3Example);
            ResponsesDict.Add(
                "https://api.nuget.org/v3/registration0/nuget.versioning/index.json",
                PowershellJsonData.VersionAutocompleteRegistrationExample);
            ResponsesDict.Add(
                "https://nuget.org/api/v2/package-ids?partialId=elm&includePrerelease=True&semVerLevel=2.0.0",
                PowershellJsonData.AutoCompleteV2Example);
            ResponsesDict.Add(
                "https://nuget.org/api/v2/package-versions/NuGet.Versioning?includePrerelease=True&semVerLevel=2.0.0",
                PowershellJsonData.VersionAutoCompleteV2Example);
        }

        [Theory]
        [InlineData("http://source.test/v3/index.json")]
        [InlineData("https://nuget.org/api/v2/")]
        public async Task PowershellAutoComplete_VersionStartsWithReturnsExpectedResults(string sourceUrl)
        {
            // Arrange
            var source = StaticHttpHandler.CreateSource(sourceUrl, Repository.Provider.GetVisualStudio(), ResponsesDict);
            var resource = await source.GetResourceAsync<AutoCompleteResource>();
            Assert.NotNull(resource);

            // Act
            using (var sourceCacheContext = new SourceCacheContext())
            {
                var versions = await resource.VersionStartsWith(
                    "NuGet.Versioning",
                    "3.",
                    includePrerelease: true,
                    sourceCacheContext: sourceCacheContext,
                    log: NullLogger.Instance,
                    token: CancellationToken.None);

                // Assert
                Assert.NotNull(versions);
                Assert.Equal(2, versions.Count());
                Assert.Contains(new NuGetVersion("3.5.0-rc1-final"), versions);
                Assert.Contains(new NuGetVersion("3.5.0"), versions);
            }
        }

        [Theory]
        [InlineData("http://source.test/v3/index.json")]
        [InlineData("https://nuget.org/api/v2/")]
        public async Task PowershellAutoComplete_IdStartsWithReturnsExpectedResults(string sourceUrl)
        {
            // Arrange
            var source = StaticHttpHandler.CreateSource(sourceUrl, Repository.Provider.GetVisualStudio(), ResponsesDict);
            var resource = await source.GetResourceAsync<AutoCompleteResource>();
            Assert.NotNull(resource);

            // Act
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            IEnumerable<string> packages = await resource.IdStartsWith("elm", true, Common.NullLogger.Instance, cancellationTokenSource.Token);

            // Assert
            Assert.True(packages != null & packages.Count() > 0);
            Assert.Contains("elmah", packages);
        }

        [Theory]
        [InlineData("http://source.test/v3/index.json")]
        [InlineData("https://nuget.org/api/v2/")]
        public async Task PowershellAutoComplete_IdStartsWithCancelsAsAppropriate(string sourceUrl)
        {
            // Arrange
            var source = StaticHttpHandler.CreateSource(sourceUrl, Repository.Provider.GetVisualStudio(), ResponsesDict);
            var resource = await source.GetResourceAsync<AutoCompleteResource>();
            Assert.NotNull(resource);

            // Act
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            Task<IEnumerable<string>> packagesTask = resource.IdStartsWith("elm", true, Common.NullLogger.Instance, cancellationTokenSource.Token);
            cancellationTokenSource.Cancel();

            // Assert
            try
            {
                packagesTask.Wait();
            }
            catch (AggregateException e)
            {
                Assert.Equal(e.InnerExceptions.Count(), 1);
                Assert.True(e.InnerExceptions.Any(item => item.GetType().Equals(typeof(TaskCanceledException))));
            }
        }
    }
}
