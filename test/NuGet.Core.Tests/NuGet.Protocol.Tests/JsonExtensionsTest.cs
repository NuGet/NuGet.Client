// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class JsonExtensionsTest
    {
        [Fact]
        public void FromJTokenWithBadUrl()
        {
            // Arrange
            var toke = JToken.Parse(JsonData.BadProjectUrlJsonData);

            // Act
            var metaData = toke.FromJToken<PackageSearchMetadata>();

            // Assert
            Assert.Null(metaData.ProjectUrl);
        }
    }
}
