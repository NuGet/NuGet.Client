using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Newtonsoft.Json.Linq;
using NuGet.Protocol;

namespace NuGet.Protocol.Tests
{
    public class JasonExtensionsTest
    {
        [Fact]
        public void FromJTokenWithBadUrl()
        {
            // Arrange
            var toke = JToken.Parse(JsonData.badProjectUrlJsonData);

            // Act
            var metaData = toke.FromJToken<PackageSearchMetadata>();

            // Assert
            Assert.Null(metaData.ProjectUrl);
        }
    }
}
