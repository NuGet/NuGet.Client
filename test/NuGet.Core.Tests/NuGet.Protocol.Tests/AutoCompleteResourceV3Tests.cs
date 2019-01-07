// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class AutoCompleteResourceV3Tests
    {
        [Fact]
        public async Task AutoCompleteResourceV3_IdStartsWithAsync()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            const string sourceName = "http://testsource.com/v3/index.json";
            responses.Add(sourceName, JsonData.IndexWithoutFlatContainer);
            responses.Add("https://api-v3search-0.nuget.org/autocomplete?q=newt&prerelease=true&semVerLevel=2.0.0",
                JsonData.AutoCompleteEndpointNewtResult);

            var repo = StaticHttpHandler.CreateSource(sourceName, Repository.Provider.GetCoreV3(), responses);
            var resource = await repo.GetResourceAsync<AutoCompleteResource>();

            // Act
            var result = resource.IdStartsWith("newt", true, NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Equal(10, result.Result.Count());
        }
    }
}
