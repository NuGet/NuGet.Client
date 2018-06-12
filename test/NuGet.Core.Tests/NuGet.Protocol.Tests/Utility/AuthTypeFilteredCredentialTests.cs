// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using Moq;
using NuGet.Common;
using NuGet.Protocol.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class AuthTypeFilteredCredentialTests
    {
        [Theory]
        [InlineData("negotiate")]
        [InlineData("basic")]
        [InlineData("somethirdthing")]
        void ApplyFilterFromEnvironmentVariable_AllowsAnyWhenEnvVarNotSet(string authType)
        {
            // Arrange
            var expected = new NetworkCredential("username", "password");
            var innerCredential = Mock.Of<ICredentials>(
                x => x.GetCredential(It.IsAny<Uri>(), It.IsAny<string>()) == expected);
            var environment = Mock.Of<IEnvironmentVariableReader>(
                x => x.GetEnvironmentVariable("NUGET_AUTHENTICATION_TYPES") == null);

            // Act
            var actual = AuthTypeFilteredCredentials
                .ApplyFilterFromEnvironmentVariable(innerCredential, environment)
                .GetCredential(new Uri("https://example.com/"), authType);

            // Assert
            Assert.Same(expected, actual);
        }

        [Theory]
        [InlineData("basic")]
        [InlineData("somethirdthing")]
        void ApplyFilterFromEnvironmentVariable_GetCredentialPassesThroughWhenAuthTypeInFilter(string authType)
        {
            // Arrange
            var expected = new NetworkCredential("username", "password");
            var innerCredential = Mock.Of<ICredentials>(
                x => x.GetCredential(It.IsAny<Uri>(), It.IsAny<string>()) == expected);
            var environment = Mock.Of<IEnvironmentVariableReader>(
                x => x.GetEnvironmentVariable("NUGET_AUTHENTICATION_TYPES") == "basic,somethirdthing");

            // Act
            var actual = AuthTypeFilteredCredentials
                .ApplyFilterFromEnvironmentVariable(innerCredential, environment)
                .GetCredential(new Uri("https://example.com/"), authType);

            // Assert
            Assert.Same(expected, actual);
        }

        [Theory]
        [InlineData("negotiate")]
        [InlineData("anotherunknownvalue")]
        void ApplyFilterFromEnvironmentVariable_GetCredentialReturnsNullWhenAuthTypeNotInFilter(string authType)
        {
            // Arrange
            var unexpected = new NetworkCredential("username", "password");
            var innerCredential = Mock.Of<ICredentials>(
                x => x.GetCredential(It.IsAny<Uri>(), It.IsAny<string>()) == unexpected);
            var environment = Mock.Of<IEnvironmentVariableReader>(
                x => x.GetEnvironmentVariable("NUGET_AUTHENTICATION_TYPES") == "basic,somethirdthing");

            // Act
            var actual = AuthTypeFilteredCredentials
                .ApplyFilterFromEnvironmentVariable(innerCredential, environment)
                .GetCredential(new Uri("https://example.com/"), authType);

            // Assert
            Assert.Null(actual);
        }
    }
}