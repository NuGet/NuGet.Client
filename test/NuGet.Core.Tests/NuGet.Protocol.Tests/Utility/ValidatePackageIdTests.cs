// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.Common;
using NuGet.Packaging;
using Xunit;

namespace NuGet.Protocol.Tests.Utility
{
    public class ValidatePackageIdTests
    {
        [Theory]
        [InlineData("../contoso")]
        [InlineData("contoso/../package")]
        [InlineData("contoso/.?///?")]
        public void Validate_InvalidId_Throws(string id)
        {
            // Arrange
            var validator = new ValidatePackageId();

            // Act & Assert
            var exception = Assert.Throws<InvalidPackageIdException>(() => validator.Validate(id));
            exception.Message.Contains(id);
        }

        [Fact]
        public void Validate_EnvironmentVariableSet_DoesNotThrow()
        {
            // Arrange
            var environment = new Mock<IEnvironmentVariableReader>();
            environment.Setup(e => e.GetEnvironmentVariable("NUGET_DISABLE_PACKAGEID_VALIDATION"))
                       .Returns("true");
            var validator = new ValidatePackageId(environment.Object);

            // Act & Assert
            // This should not throw for an invalid package ID
            validator.Validate("contoso/../package");
            Assert.True(true);
        }
    }
}
