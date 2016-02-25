// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.Configuration
{
    public class PackageSourceTest
    {
        [Fact]
        public void Clone_CopiesAllPropertyValuesFromSource()
        {
            // Arrange
            var source = new PackageSource("Source", "SourceName", isEnabled: false)
                {
                    IsPasswordClearText = true,
                    PasswordText = "password",
                    UserName = "username",
                    ProtocolVersion = 43,
                };

            // Act
            var result = source.Clone();

            // Assert
            Assert.Equal(source.Source, result.Source);
            Assert.Equal(source.Name, result.Name);
            Assert.Equal(source.IsEnabled, result.IsEnabled);
            Assert.Equal(source.ProtocolVersion, result.ProtocolVersion);
            Assert.Equal(source.UserName, result.UserName);
            Assert.Equal(source.Password, source.Password);
        }
    }
}
