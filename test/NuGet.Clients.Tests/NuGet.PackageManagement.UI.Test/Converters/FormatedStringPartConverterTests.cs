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
        [InlineData("{0} will replace the deprecated package", "", " will replace the deprecated package")]
        [InlineData("Пакет устарел. Вместо этого используйте {0}.", "Пакет устарел. Вместо этого используйте ", ".")]
        [InlineData("包装被弃用。改用{0}", "包装被弃用。改用", "")]
        [InlineData("No placeholder ", null, null)]
        [InlineData(1, null, null)]
        public void FormatedStringPartConverter_HappyPaths_Succeeds(object resourceString, string expectedLeft, string expectedRight)
        {
            var converter = new FormatedStringPartConverter();

            object partLeft = converter.Convert(resourceString, targetType: null, parameter: 0, culture: null);
            object partRight = converter.Convert(resourceString, targetType: null, parameter: 2, culture: null);
            object unknownPart = converter.Convert(resourceString, targetType: null, parameter: "whatever", culture: null);
            object numberPart = converter.Convert(resourceString, targetType: null, parameter: 50, culture: null);
            Assert.Equal(expectedLeft, partLeft);
            Assert.Equal(expectedRight, partRight);
            Assert.Null(unknownPart);
            Assert.Null(numberPart);
        }
    }
}
