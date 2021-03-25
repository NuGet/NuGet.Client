// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public void AcceleratorKeyConverter_Localized_ReturnsNotEmpty(string input, string expected)
        {
            var converter = new AcceleratorKeyConverter();

            var actual = converter.Convert(input, null, null, null);

            Assert.Equal(expected, actual);
        }
    }
}
