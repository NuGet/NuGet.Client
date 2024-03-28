// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using FluentAssertions;
using NuGet.ContentModel;
using Xunit;

namespace NuGet.Packaging.Test.ContentModelTests
{
    public class ReadOnlyMemoryCharComparerOrdinalTests
    {
        [Theory]
        [InlineData("ABCDEFG", "ABCDEFG", true)]
        [InlineData("ABCDEFG", "abcdefg", false)]
        [InlineData("ref/four/foo.dll", "lib/four/bar.dll", true, 4, 4)]
        [InlineData("ref/four/foo.dll", "lib/five/bar.dll", false, 4, 4)]
        public void Equals_ReturnsExpectedValue(string x, string y, bool expected, int? start = default, int? length = default)
        {
            ReadOnlyMemoryCharComparerOrdinal.Instance.Equals(x.AsMemory(start ?? 0, length ?? x.Length), y.AsMemory(start ?? 0, length ?? y.Length)).Should().Be(expected);
        }

        [Theory]
        [InlineData("ABCDEFG", "ABCDEFG", true)]
        [InlineData("ABCDEFGH", "ABCDefgH", false)]
        [InlineData("ref/four/foo.dll", "lib/four/bar.dll", true, 4, 4)]
        [InlineData("ref/four/foo.dll", "lib/five/bar.dll", false, 4, 4)]
        public void GetHashCode_ReturnsExpectedValue(string x, string y, bool expected, int? start = default, int? length = default)
        {
            int hashX = ReadOnlyMemoryCharComparerOrdinal.Instance.GetHashCode(x.AsMemory(start ?? 0, length ?? x.Length));
            int hashY = ReadOnlyMemoryCharComparerOrdinal.Instance.GetHashCode(y.AsMemory(start ?? 0, length ?? y.Length));

            hashX.Equals(hashY).Should().Be(expected, $"hashX is {hashX} and hashY is {hashY}");
        }
    }
}
