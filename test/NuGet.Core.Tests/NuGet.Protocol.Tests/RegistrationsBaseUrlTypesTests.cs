// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using FluentAssertions;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class RegistrationsBaseUrlTypesTests
    {
        [Fact]
        public void RegistrationsBaseUrlTypes_RegistrationsBaseUrl_Has_Correct_Coresponding_String_Literal()
        {
            RegistrationsBaseUrlTypes.RegistrationsBaseUrl.Should().Be("RegistrationsBaseUrl");
        }

        [Fact]
        public void RegistrationsBaseUrlTypes_RegistrationsBaseUrl300beta_Has_Correct_Coresponding_String_Literal()
        {
            RegistrationsBaseUrlTypes.RegistrationsBaseUrlVersion300beta.Should().Be("RegistrationsBaseUrl/3.0.0-beta");
        }

        [Fact]
        public void RegistrationsBaseUrlTypes_RegistrationsBaseUrl300rc_Has_Correct_Coresponding_String_Literal()
        {
            RegistrationsBaseUrlTypes.RegistrationsBaseUrlVersion300rc.Should().Be("RegistrationsBaseUrl/3.0.0-rc");
        }

        [Fact]
        public void RegistrationsBaseUrlTypes_RegistrationsBaseUrl340_Has_Correct_Coresponding_String_Literal()
        {
            RegistrationsBaseUrlTypes.RegistrationsBaseUrlVersion340.Should().Be("RegistrationsBaseUrl/3.4.0");
        }

        [Fact]
        public void RegistrationsBaseUrlTypes_RegistrationsBaseUrl360_Has_Correct_Coresponding_String_Literal()
        {
            RegistrationsBaseUrlTypes.RegistrationsBaseUrlVersion360.Should().Be("RegistrationsBaseUrl/3.6.0");
        }

        [Fact]
        public void RegistrationsBaseUrlTypes_RegistrationsBaseUrlVersioned_Has_Correct_Coresponding_String_Literal()
        {
            RegistrationsBaseUrlTypes.RegistrationsBaseUrlVersioned.Should().Be("RegistrationsBaseUrl/Versioned");
        }

        [Fact]
        public void RegistrationsBaseUrlTypes_RegistrationsBaseUrls_Returns_All_Versions_In_Desc_Order()
        {
            string[] expected =
            {
                "RegistrationsBaseUrl/Versioned",
                "RegistrationsBaseUrl/3.6.0",
                "RegistrationsBaseUrl/3.4.0",
                "RegistrationsBaseUrl/3.0.0-rc",
                "RegistrationsBaseUrl/3.0.0-beta",
                "RegistrationsBaseUrl"
            };
            RegistrationsBaseUrlTypes.RegistrationsBaseUrls.Should().ContainInOrder(expected);
        }
    }
}
