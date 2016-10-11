// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class MSBuildRestoreImportGroupTests
    {
        [Fact]
        public void MSBuildRestoreImportGroupTest_VerifyEmptyCondition()
        {
            // Arrange
            var group = new MSBuildRestoreImportGroup();

            // Act
            var condition = group.Condition;

            // Assert
            Assert.Equal(string.Empty, condition);
        }

        [Fact]
        public void MSBuildRestoreImportGroupTest_SingleCondition()
        {
            // Arrange
            var group = new MSBuildRestoreImportGroup();
            group.Conditions.Add("'$(a)' == 'a'  ");

            // Act
            var condition = group.Condition;

            // Assert
            Assert.Equal(" '$(a)' == 'a' ", condition);
        }

        [Fact]
        public void MSBuildRestoreImportGroupTest_MultipleConditions()
        {
            // Arrange
            var group = new MSBuildRestoreImportGroup();
            group.Conditions.Add("'$(b)' != 'b'  ");
            group.Conditions.Add("    '$(a)' == 'a'  ");

            // Act
            var condition = group.Condition;

            // Assert
            Assert.Equal(" '$(b)' != 'b' AND '$(a)' == 'a' ", condition);
        }
    }
}
