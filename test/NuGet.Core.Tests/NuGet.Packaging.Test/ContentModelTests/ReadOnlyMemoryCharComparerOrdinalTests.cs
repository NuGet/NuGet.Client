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
        [InlineData("AbCDeFg", "AbCDeFg", true)]
        [InlineData("AbCDeFG", "abcdefg", false)]
        public void ReadOnlyMemoryCharComparerOrdinal_ReturnsExpectedValue(string x, string y, bool expected)
        {
            if (expected)
            {
                ReadOnlyMemoryCharComparerOrdinal.Instance.Equals(x.AsMemory(), y.AsMemory()).Should().BeTrue();
            }
            else
            {
                ReadOnlyMemoryCharComparerOrdinal.Instance.Equals(x.AsMemory(), y.AsMemory()).Should().BeFalse();
            }
        }
    }
}
