// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.VisualStudio.Implementation.Test.Extensibility
{
    public class VsSemanticVersionTests
    {
        [Fact]
        public void SemanticVersion_ParseVersion_ThrowsIfNullOrEmpty()
        {
            var nullException = Assert.Throws<ArgumentException>(paramName: "version", () => SemanticVersion.Parse(null));
            var emptyException = Assert.Throws<ArgumentException>(paramName: "version", () => SemanticVersion.Parse(string.Empty));

            Assert.StartsWith("Value cannot be null or an empty string.", nullException.Message);
            Assert.StartsWith("Value cannot be null or an empty string.", emptyException.Message);
        }

        [Theory]
        [InlineData(" ")]
        [InlineData("invalid")]
        [InlineData("1.1.1.1.1")]
        [InlineData("%")]
        public void SemanticVersion_ParseVersion_ThrowsIfInvalid(string invalidVersion)
        {
            var invalidException = Assert.Throws<ArgumentException>(paramName: "version", () => SemanticVersion.Parse(invalidVersion));

            Assert.StartsWith("'" + invalidVersion + "' is not a valid version string.", invalidException.Message);
        }
    }
}
