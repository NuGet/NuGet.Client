// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using FluentAssertions;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class RegistrationsBaseUrlTypesTests
    {
        [Fact]
        public void RegistrationsBaseUrlTypes_RegistrationsBaseUrl_Contains_AllAliases()
        {
            RegistrationsBaseUrlTypes.RegistrationsBaseUrl.Should().Contain("RegistrationsBaseUrl");
            RegistrationsBaseUrlTypes.RegistrationsBaseUrl.Should().Contain("RegistrationsBaseUrl/3.0.0-beta");
            RegistrationsBaseUrlTypes.RegistrationsBaseUrl.Should().Contain("RegistrationsBaseUrl/3.0.0-rc");
        }

        [Fact]
        public void RegistrationsBaseUrlTypes_RegistrationsBaseUrlVersion340_OnlyVersion340()
        {
            RegistrationsBaseUrlTypes.RegistrationsBaseUrlVersion340.Should().Contain("RegistrationsBaseUrl/3.4.0");
        }

        [Fact]
        public void RegistrationsBaseUrlTypes_RegistrationsBaseUrlVersion360_Contains_AllAliases()
        {
            RegistrationsBaseUrlTypes.RegistrationsBaseUrl.Should().Contain("RegistrationsBaseUrl/3.6.0");
            RegistrationsBaseUrlTypes.RegistrationsBaseUrl.Should().Contain("RegistrationsBaseUrl/Versioned");
        }
    }
}
