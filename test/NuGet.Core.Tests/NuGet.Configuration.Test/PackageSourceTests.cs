// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using FluentAssertions;
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

            // source data
            Assert.Equal(source.Source, result.Source);
            Assert.Equal(source.Name, result.Name);
            Assert.Equal(source.IsEnabled, result.IsEnabled);
            Assert.Equal(source.ProtocolVersion, result.ProtocolVersion);

            // source credential
            result.Credentials.Should().NotBeNull();
            result.Credentials.Source.ShouldBeEquivalentTo(source.Credentials.Source);
            result.Credentials.Username.ShouldBeEquivalentTo(source.Credentials.Username);
            result.Credentials.IsPasswordClearText.ShouldBeEquivalentTo(source.Credentials.IsPasswordClearText);
        }
    }
}
