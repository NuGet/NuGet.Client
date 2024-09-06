// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Windows;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.Converters
{
    public class IsGreaterThanToVisibilityConverterTests
    {
        [Theory]
        [InlineData(0L)]
        [InlineData(1L)]
        [InlineData(2147483657L)]
        public void Convert_ValidLongValue_Return_VisibilityVisible(long? downloadCount)
        {
            var converter = new IsGreaterThanToVisibilityConverter();

            object converted = converter.Convert(
                downloadCount,
                typeof(Visibility),
                "-1",
                Thread.CurrentThread.CurrentCulture);

            Assert.Equal(Visibility.Visible, converted);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5000)]
        public void Convert_ValidInt_Return_VisibilityVisible(int? downloadCount)
        {
            var converter = new IsGreaterThanToVisibilityConverter();

            object converted = converter.Convert(
                downloadCount,
                typeof(Visibility),
                "0",
                Thread.CurrentThread.CurrentCulture);

            Assert.Equal(Visibility.Visible, converted);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(-1L)]
        public void Convert_ValidLongValue_Return_VisibilityCollapsed(long? downloadCount)
        {
            var converter = new IsGreaterThanToVisibilityConverter();

            object converted = converter.Convert(
                downloadCount,
                typeof(Visibility),
                "-1",
                Thread.CurrentThread.CurrentCulture);

            Assert.Equal(Visibility.Collapsed, converted);
        }
    }
}
