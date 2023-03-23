// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using FluentAssertions;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class ClientVersionsTests
    {
        [Fact]
        public void ClientVersions_Version200_Has_Correct_Coresponding_String_Literal()
        {
            var expectedVersion = "/2.0.0";

            ClientVersions.Version200.Should().Be(expectedVersion);
        }

        [Fact]
        public void ClientVersions_Version300_Has_Correct_Coresponding_String_Literal()
        {
            var expectedVersion = "/3.0.0";

            ClientVersions.Version300.Should().Be(expectedVersion);
        }

        [Fact]
        public void ClientVersions_Version300beta_Has_Correct_Coresponding_String_Literal()
        {
            var expectedVersion = "/3.0.0-beta";

            ClientVersions.Version300beta.Should().Be(expectedVersion);
        }

        [Fact]
        public void ClientVersions_Version300rc_Has_Correct_Coresponding_String_Literal()
        {
            var expectedVersion = "/3.0.0-rc";

            ClientVersions.Version300rc.Should().Be(expectedVersion);
        }

        [Fact]
        public void ClientVersions_Version340_Has_Correct_Coresponding_String_Literal()
        {
            var expectedVersion = "/3.4.0";

            ClientVersions.Version340.Should().Be(expectedVersion);
        }

        [Fact]
        public void ClientVersions_Version360_Has_Correct_Coresponding_String_Literal()
        {
            var expectedVersion = "/3.6.0";

            ClientVersions.Version360.Should().Be(expectedVersion);
        }

        [Fact]
        public void ClientVersions_Version470_Has_Correct_Coresponding_String_Literal()
        {
            var expectedVersion = "/4.7.0";

            ClientVersions.Version470.Should().Be(expectedVersion);
        }

        [Fact]
        public void ClientVersions_Version490_Has_Correct_Coresponding_String_Literal()
        {
            var expectedVersion = "/4.9.0";

            ClientVersions.Version490.Should().Be(expectedVersion);
        }

        [Fact]
        public void ClientVersions_Version500_Has_Correct_Coresponding_String_Literal()
        {
            var expectedVersion = "/5.0.0";

            ClientVersions.Version500.Should().Be(expectedVersion);
        }

        [Fact]
        public void ClientVersions_Version510_Has_Correct_Coresponding_String_Literal()
        {
            var expectedVersion = "/5.1.0";

            ClientVersions.Version510.Should().Be(expectedVersion);
        }
    }
}
