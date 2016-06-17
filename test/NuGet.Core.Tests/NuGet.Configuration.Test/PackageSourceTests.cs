// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.Configuration
{
    public class PackageSourceTests
    {
        [Fact]
        public void Clone_CopiesAllPropertyValuesFromSource()
        {
            // Arrange
            var credentials = new PackageSourceCredential("SourceName", "username", "password", isPasswordClearText: false);
            var source = new PackageSource("Source", "SourceName", isEnabled: false)
                {
                    Credentials = credentials,
                    ProtocolVersion = 43
                };

            // Act
            var result = source.Clone();

            // Assert
            Assert.Equal(source.Source, result.Source);
            Assert.Equal(source.Name, result.Name);
            Assert.Equal(source.IsEnabled, result.IsEnabled);
            Assert.Equal(source.ProtocolVersion, result.ProtocolVersion);
            Assert.Same(source.Credentials, result.Credentials);
        }
    }
}
