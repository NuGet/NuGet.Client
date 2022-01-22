// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.VisualStudio;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.Converters
{
    public class PackageLevelTypeToGroupNameConverterTests
    {
        public static IEnumerable<object[]> GetConvertData()
        {
            yield return new object[] { PackageLevelType.TopLevel, Resources.PackageLevelType_TopLevelPackageHeaderText };
            yield return new object[] { PackageLevelType.Transitive, Resources.PackageLevelType_TransitivePackageHeaderText };
            yield return new object[] { Resources.PackageLevelType_TopLevelPackageHeaderText, null };
            yield return new object[] { "some string", null };
            yield return new object[] { new object(), null };
            yield return new object[] { 12345, null };
            yield return new object[] { null, null };
        }

        public static IEnumerable<object[]> GetConvertBackData()
        {
            yield return new object[] { Resources.PackageLevelType_TopLevelPackageHeaderText, PackageLevelType.TopLevel };
            yield return new object[] { Resources.PackageLevelType_TransitivePackageHeaderText, PackageLevelType.Transitive };
            yield return new object[] { PackageLevelType.TopLevel, null };
            yield return new object[] { "some string", null };
            yield return new object[] { new object(), null };
            yield return new object[] { 12345, null };
            yield return new object[] { null, null };
        }

        [Theory]
        [MemberData(nameof(GetConvertData))]
        public void Convert_MultipleInputs_Succeeds(object input, object expected)
        {
            var converterToTest = new PackageLevelTypeToGroupNameConverter();

            object value = converterToTest.Convert(input, targetType: null, parameter: null, culture: null);

            Assert.Equal(expected, value);
        }

        [Theory]
        [MemberData(nameof(GetConvertBackData))]
        public void ConvertBack_MultipleInputs_Succeeds(object input, object expected)
        {
            var converterToTest = new PackageLevelTypeToGroupNameConverter();

            object value = converterToTest.ConvertBack(input, targetType: null, parameter: null, culture: null);

            Assert.Equal(expected, value);
        }
    }
}
