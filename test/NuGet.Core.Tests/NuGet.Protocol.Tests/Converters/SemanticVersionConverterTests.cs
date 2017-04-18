// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class SemanticVersionConverterTests
    {
        private readonly SemanticVersionConverter _converter;

        public SemanticVersionConverterTests()
        {
            _converter = new SemanticVersionConverter();
        }

        [Fact]
        public void CanConvert_ReturnsTrueForSemanticVersionType()
        {
            var canConvert = _converter.CanConvert(typeof(SemanticVersion));

            Assert.True(canConvert);
        }

        [Fact]
        public void CanConvert_ReturnsFalseForNonSemanticVersionType()
        {
            var canConvert = _converter.CanConvert(typeof(DateTime));

            Assert.False(canConvert);
        }
    }
}