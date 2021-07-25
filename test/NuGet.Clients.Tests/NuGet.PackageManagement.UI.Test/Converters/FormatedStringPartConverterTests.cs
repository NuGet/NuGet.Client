// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.PackageManagement.UI.Test.Converters
{
    public class FormatedStringPartConverterTests
    {
        [Theory]
        [InlineData("Package deprecated. Alternative {0}", "Package deprecated. Alternative ", "")]
        [InlineData("Deprecated. Use {0} instead", "Deprecated. Use ", " instead")]
        [InlineData("{0} will replace the deprecated package", "", " will replace deprecated package")]
        [InlineData("Пакет устарел. Вместо этого используйте {0}.", "Пакет устарел. Вместо этого используйте ", ".")]
        [InlineData("包装被弃用。改用{0}", "包装被弃用。改用", "")]
        public void FormatedStringPartConverter_HappyPath_Succeeds(string resourceString, string expectedLeft, string expectedRight)
        {
            var converter = new FormatedStringPartConverter();

            var partLeft = converter.Convert(resourceString, targetType: null, parameter: 0, culture: null);
            var partRight = converter.Convert(resourceString, targetType: null, parameter: 2, culture: null);
            Assert.Equal(expectedLeft, partLeft);
            Assert.Equal(expectedRight, partRight);
        }
    }
}
