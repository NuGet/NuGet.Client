// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;
using FluentAssertions;
using NuGet.Packaging.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class Crc32Tests
    {
        [Fact]
        public void Crc32_SameOutputForSameData()
        {
            // Arrange
            var data = Encoding.ASCII.GetBytes("Test data");

            // Act
            var code1 = Crc32.CalculateCrc(data);
            var code2 = Crc32.CalculateCrc(data);
            var code3 = Crc32.CalculateCrc(data);

            // Assert
            code1.Should().Be(code2);
            code1.Should().Be(code3);
        }

        [Fact]
        public void Crc32_DifferentOutputForDifferentData()
        {
            // Arrange
            var data1 = Encoding.ASCII.GetBytes("Test data1");
            var data2 = Encoding.ASCII.GetBytes("Test data2");

            // Act
            var code1 = Crc32.CalculateCrc(data1);
            var code2 = Crc32.CalculateCrc(data2);

            // Assert
            code1.Should().NotBe(code2);
        }
    }
}
