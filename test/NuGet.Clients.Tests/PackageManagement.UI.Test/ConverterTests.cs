// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.PackageManagement.UI;
using Xunit;

namespace PackageManagement.UI.Test
{
    public class ConverterTests
    {
        [Theory]
        [InlineData(1, "1")]
        [InlineData(999, "999")]
        [InlineData(1000, "1K")]
        [InlineData(1200, "1.2K")]
        [InlineData(1230, "1.23K")]

        // at most 3 significant digits
        [InlineData(1234, "1.23K")]

        // there is rounding
        [InlineData(1239, "1.24K")]

        [InlineData(123400, "123K")]
        [InlineData(1234000, "1.23M")]
        [InlineData(12340000, "12.3M")]
        [InlineData(1234000000, "1.23G")]
        public void DownloadCountToStringTest(int num, string expected)
        {
            var s = UIUtility.NumberToString(num);
            Assert.Equal(expected, s);
        }
    }
}
