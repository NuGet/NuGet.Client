// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Moq;
using Xunit;

namespace NuGet.Commands.Test
{
    public class LanguageEnvirionmentVariableTests
    {
        [Fact]
        public void Constructor_NullArguments_Throws()
        {
            Assert.Throws<ArgumentException>(() => new LanguageEnvironmentVariable(null, It.IsAny<Func<string, CultureInfo>>(), It.IsAny<Func<CultureInfo, string>>()));
            Assert.Throws<ArgumentException>(() => new LanguageEnvironmentVariable(string.Empty, It.IsAny<Func<string, CultureInfo>>(), It.IsAny<Func<CultureInfo, string>>()));
            Assert.Throws<ArgumentException>(() => new LanguageEnvironmentVariable("   ", It.IsAny<Func<string, CultureInfo>>(), It.IsAny<Func<CultureInfo, string>>()));

            Assert.Throws<ArgumentNullException>(() => new LanguageEnvironmentVariable("ENVVAR", generatorFunc: null, It.IsAny<Func<CultureInfo, string>>()));

            Assert.Throws<ArgumentNullException>(() => new LanguageEnvironmentVariable("ENVAR", It.IsAny<Func<string, CultureInfo>>(), valueFunc: null));
        }

        [Fact]
        public void GetCultureFromName_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LanguageEnvironmentVariable.GetCultureFromName(envvarValue: null) );
        }

        [Fact]
        public void GetCultureFromLCID_NullArgument_ReturnsNull()
        {
            Assert.Null(LanguageEnvironmentVariable.GetCultureFromLCID(envvarValue: null));
            Assert.Null(LanguageEnvironmentVariable.GetCultureFromLCID(envvarValue: ""));
            Assert.Null(LanguageEnvironmentVariable.GetCultureFromLCID(envvarValue: "   "));
        }

        [Fact]
        public void CultureToName_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LanguageEnvironmentVariable.CultureToName(culture: null));
        }

        [Fact]
        public void CultureToName_NeutralCultureInfo_ReturnsEmpty()
        {
            // Arrange
            var cultureInfo = new CultureInfo(string.Empty);

            // Act
            var result = LanguageEnvironmentVariable.CultureToName(cultureInfo);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void CultureToLCID_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LanguageEnvironmentVariable.CultureToLCID(culture: null));
        }

        [Fact]
        public void CultureToLCID_NeutralCultureInfo_Returns127()
        {
            // Arrange
            var cultureInfo = new CultureInfo(string.Empty);

            // Act
            var result = LanguageEnvironmentVariable.CultureToLCID(cultureInfo);

            // Assert
            Assert.Equal("127", result); // 127 is the neutral CultureInfo LCID
        }
    }
}
