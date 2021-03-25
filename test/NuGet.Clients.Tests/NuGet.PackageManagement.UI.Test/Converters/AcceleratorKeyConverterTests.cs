// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Moq;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.Converters
{
    public class AcceleratorKeyConverterTests
    {
        [Theory]
        [InlineData("_Yes", "Y")]
        [InlineData("No to _All", "A")]
        [InlineData("Нет для в_сех", "с")]
        [InlineData("", "")]
        [InlineData(null, "")]
        [InlineData(1, "")]
        public void AcceleratorKeyConverter_Localized_ReturnsNotEmpty(object input, object expected)
        {
            var converter = new AcceleratorKeyConverter();

            var actual = converter.Convert(input, It.IsAny<Type>(), It.IsAny<object>(), It.IsAny<CultureInfo>());

            Assert.Equal(expected, actual);
        }
    }
}
