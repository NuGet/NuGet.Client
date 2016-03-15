using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;
using NuGet.Protocol.Core.Types;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class AutoCompleteResourceV2FeedTests
    {
        [Fact]
        public async Task AutoCompleteResourceV2Feed_IdStartsWith()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource/v2/package-ids?partialId=Azure&includePrerelease=False",
                 TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.AzureAutoComplete.json", GetType()));

            var repo = StaticHttpHandler.CreateSource("http://testsource/v2/", Repository.Provider.GetCoreV3(), responses);

            var autoCompleteResource = await repo.GetResourceAsync<AutoCompleteResource>();

            // Act
            var result = await autoCompleteResource.IdStartsWith("Azure", false, NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Equal(30, result.Count());
        }

        [Fact]
        public async Task AutoCompleteResourceV2Feed_VersionStartsWith()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource/v2/package-versions/xunit?includePrerelease=False",
                 TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.XunitVersionAutoComplete.json", GetType()));

            var repo = StaticHttpHandler.CreateSource("http://testsource/v2/", Repository.Provider.GetCoreV3(), responses);

            var autoCompleteResource = await repo.GetResourceAsync<AutoCompleteResource>();

            // Act
            var result = await autoCompleteResource.VersionStartsWith("xunit", "1", false, NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Equal(6, result.Count());
        }

        [Fact]
        public async Task AutoCompleteResourceV2Feed_VersionStartsWithInvalidId()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource/v2/package-versions/azure?includePrerelease=False", "[]");

            var repo = StaticHttpHandler.CreateSource("http://testsource/v2/", Repository.Provider.GetCoreV3(), responses);

            var autoCompleteResource = await repo.GetResourceAsync<AutoCompleteResource>();

            // Act
            var result = await autoCompleteResource.VersionStartsWith("azure", "1", false, NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Equal(0, result.Count());
        }
    }
}
