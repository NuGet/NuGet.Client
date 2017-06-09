// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class WarningPropertiesCollectionTests
    {

        [Fact]
        public void WarningPropertiesCollection_ProjectPropertiesWithNoWarn()
        {
            // Arrange
            var noWarnSet = new HashSet<NuGetLogCode> { NuGetLogCode.NU1500 };
            var warnAsErrorSet = new HashSet<NuGetLogCode> { };
            var allWarningsAsErrors = false;
            var warningPropertiesCollection = new WarningPropertiesCollection()
            {
                ProjectWideWarningProperties = new WarningProperties(warnAsErrorSet, noWarnSet, allWarningsAsErrors)
            };

            var suppressedMessage = new RestoreLogMessage(LogLevel.Warning, NuGetLogCode.NU1500, "Warning");
            var nonSuppressedMessage = new RestoreLogMessage(LogLevel.Warning, NuGetLogCode.NU1601, "Warning");

            // Act && Assert
            Assert.True(warningPropertiesCollection.ApplyWarningProperties(suppressedMessage));
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(nonSuppressedMessage));
            Assert.Equal(LogLevel.Warning, nonSuppressedMessage.Level);
        }

        [Fact]
        public void WarningPropertiesCollection_ProjectPropertiesWithWarnAsError()
        {
            // Arrange
            var noWarnSet = new HashSet<NuGetLogCode> { };
            var warnAsErrorSet = new HashSet<NuGetLogCode> { NuGetLogCode.NU1500 };
            var allWarningsAsErrors = false;
            var warningPropertiesCollection = new WarningPropertiesCollection()
            {
                ProjectWideWarningProperties = new WarningProperties(warnAsErrorSet, noWarnSet, allWarningsAsErrors)
            };

            var upgradedMessage = new RestoreLogMessage(LogLevel.Warning, NuGetLogCode.NU1500, "Warning");
            var nonSuppressedMessage = new RestoreLogMessage(LogLevel.Warning, NuGetLogCode.NU1601, "Warning");

            // Act && Assert
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(upgradedMessage));
            Assert.Equal(LogLevel.Error, upgradedMessage.Level);
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(nonSuppressedMessage));
            Assert.Equal(LogLevel.Warning, nonSuppressedMessage.Level);
        }

        [Fact]
        public void WarningPropertiesCollection_ProjectPropertiesWithWarnAsErrorAndUndefinedWarningCode()
        {
            // Arrange
            var noWarnSet = new HashSet<NuGetLogCode> { };
            var warnAsErrorSet = new HashSet<NuGetLogCode> { NuGetLogCode.Undefined };
            var allWarningsAsErrors = false;
            var warningPropertiesCollection = new WarningPropertiesCollection()
            {
                ProjectWideWarningProperties = new WarningProperties(warnAsErrorSet, noWarnSet, allWarningsAsErrors)
            };

            var nonSuppressedMessage = new RestoreLogMessage(LogLevel.Warning, NuGetLogCode.Undefined, "Warning");
            var nonSuppressedMessage2 = new RestoreLogMessage(LogLevel.Warning, NuGetLogCode.NU1601, "Warning");

            // Act && Assert
            // WarningPropertiesCollection should not Upgrade Warnings with Undefined code.
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(nonSuppressedMessage));
            Assert.Equal(LogLevel.Error, nonSuppressedMessage.Level);
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(nonSuppressedMessage2));
            Assert.Equal(LogLevel.Warning, nonSuppressedMessage2.Level);
        }

        [Fact]
        public void WarningPropertiesCollection_ProjectPropertiesWithAllWarningsAsErrors()
        {
            // Arrange
            var noWarnSet = new HashSet<NuGetLogCode> { };
            var warnAsErrorSet = new HashSet<NuGetLogCode> { };
            var allWarningsAsErrors = true;
            var warningPropertiesCollection = new WarningPropertiesCollection()
            {
                ProjectWideWarningProperties = new WarningProperties(warnAsErrorSet, noWarnSet, allWarningsAsErrors)
            };

            var upgradedMessage = new RestoreLogMessage(LogLevel.Warning, NuGetLogCode.NU1500, "Warning");
            var upgradedMessage2 = new RestoreLogMessage(LogLevel.Warning, NuGetLogCode.NU1601, "Warning");

            // Act && Assert
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(upgradedMessage));
            Assert.Equal(LogLevel.Error, upgradedMessage.Level);
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(upgradedMessage2));
            Assert.Equal(LogLevel.Error, upgradedMessage2.Level);
        }

        [Fact]
        public void WarningPropertiesCollection_ProjectPropertiesWithAllWarningsAsErrorsAndWarningWithUndefinedCode()
        {
            // Arrange
            var noWarnSet = new HashSet<NuGetLogCode> { };
            var warnAsErrorSet = new HashSet<NuGetLogCode> { };
            var allWarningsAsErrors = true;
            var warningPropertiesCollection = new WarningPropertiesCollection()
            {
                ProjectWideWarningProperties = new WarningProperties(warnAsErrorSet, noWarnSet, allWarningsAsErrors)
            };

            var upgradedMessage = new RestoreLogMessage(LogLevel.Warning, NuGetLogCode.Undefined, "Warning");
            var upgradedMessage2 = new RestoreLogMessage(LogLevel.Warning, NuGetLogCode.NU1601, "Warning");

            // Act && Assert
            // WarningPropertiesCollection should not Upgrade Warnings with Undefined code.
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(upgradedMessage));
            Assert.Equal(LogLevel.Warning, upgradedMessage.Level);
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(upgradedMessage2));
            Assert.Equal(LogLevel.Error, upgradedMessage2.Level);
        }

        [Fact]
        public void WarningPropertiesCollection_ProjectPropertiesWithNoWarnAndWarnAsError()
        {
            // Arrange
            var noWarnSet = new HashSet<NuGetLogCode> { NuGetLogCode.NU1500 };
            var warnAsErrorSet = new HashSet<NuGetLogCode> { NuGetLogCode.NU1500 };
            var allWarningsAsErrors = false;
            var warningPropertiesCollection = new WarningPropertiesCollection()
            {
                ProjectWideWarningProperties = new WarningProperties(warnAsErrorSet, noWarnSet, allWarningsAsErrors)
            };

            var suppressedMessage = new RestoreLogMessage(LogLevel.Warning, NuGetLogCode.NU1500, "Warning");
            var nonSuppressedMessage = new RestoreLogMessage(LogLevel.Warning, NuGetLogCode.NU1601, "Warning");

            // Act && Assert
            Assert.True(warningPropertiesCollection.ApplyWarningProperties(suppressedMessage));
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(nonSuppressedMessage));
            Assert.Equal(LogLevel.Warning, nonSuppressedMessage.Level);
        }

        [Fact]
        public void WarningPropertiesCollection_ProjectPropertiesWithNoWarnAndWarnAsErrorAndAllWarningsAsErrors()
        {
            // Arrange
            var noWarnSet = new HashSet<NuGetLogCode> { NuGetLogCode.NU1500 };
            var warnAsErrorSet = new HashSet<NuGetLogCode> { NuGetLogCode.NU1500 };
            var allWarningsAsErrors = true;
            var warningPropertiesCollection = new WarningPropertiesCollection()
            {
                ProjectWideWarningProperties = new WarningProperties(warnAsErrorSet, noWarnSet, allWarningsAsErrors)
            };

            var suppressedMessage = new RestoreLogMessage(LogLevel.Warning, NuGetLogCode.NU1500, "Warning");
            var nonSuppressedMessage = new RestoreLogMessage(LogLevel.Warning, NuGetLogCode.NU1601, "Warning");

            // Act && Assert
            Assert.True(warningPropertiesCollection.ApplyWarningProperties(suppressedMessage));
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(nonSuppressedMessage));
            Assert.Equal(LogLevel.Error, nonSuppressedMessage.Level);
        }

        [Fact]
        public void WarningPropertiesCollection_ProjectPropertiesWithWarnAsErrorAndAllWarningsAsErrors()
        {
            // Arrange
            var noWarnSet = new HashSet<NuGetLogCode> { };
            var warnAsErrorSet = new HashSet<NuGetLogCode> { NuGetLogCode.NU1500 };
            var allWarningsAsErrors = true;
            var warningPropertiesCollection = new WarningPropertiesCollection()
            {
                ProjectWideWarningProperties = new WarningProperties(warnAsErrorSet, noWarnSet, allWarningsAsErrors)
            };

            var upgradedMessage = new RestoreLogMessage(LogLevel.Warning, NuGetLogCode.NU1500, "Warning");
            var upgradedMessage2 = new RestoreLogMessage(LogLevel.Warning, NuGetLogCode.NU1601, "Warning");

            // Act && Assert
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(upgradedMessage));
            Assert.Equal(LogLevel.Error, upgradedMessage.Level);
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(upgradedMessage2));
            Assert.Equal(LogLevel.Error, upgradedMessage2.Level);
        }

        [Fact]
        public void WarningPropertiesCollection_PackagePropertiesWithFrameworkAndWarningWithFramework()
        {
            // Arrange
            // Arrange
            var libraryId = "test_library";
            var frameworkString = "net45";
            var targetFramework = NuGetFramework.Parse(frameworkString);

            var packageSpecificWarningProperties = new PackageSpecificWarningProperties();
            packageSpecificWarningProperties.Add(NuGetLogCode.NU1500, libraryId, targetFramework);

            var warningPropertiesCollection = new WarningPropertiesCollection()
            {
                PackageSpecificWarningProperties = packageSpecificWarningProperties
            };

            var suppressedMessage = RestoreLogMessage.CreateWarning(NuGetLogCode.NU1500, "Warning", libraryId, frameworkString);
            var nonSuppressedMessage = RestoreLogMessage.CreateWarning(NuGetLogCode.NU1601, "Warning", libraryId, frameworkString);

            // Act && Assert
            Assert.True(warningPropertiesCollection.ApplyWarningProperties(suppressedMessage));
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(nonSuppressedMessage));
            Assert.Equal(LogLevel.Warning, nonSuppressedMessage.Level);
        }

        [Fact]
        public void WarningPropertiesCollection_PackagePropertiesWithPerFrameworkAndWarningWithoutFramework()
        {
            // Arrange
            // Arrange
            var libraryId = "test_library";
            var frameworkString = "net45";
            var targetFramework = NuGetFramework.Parse(frameworkString);

            var packageSpecificWarningProperties = new PackageSpecificWarningProperties();
            packageSpecificWarningProperties.Add(NuGetLogCode.NU1500, libraryId, targetFramework);

            var warningPropertiesCollection = new WarningPropertiesCollection()
            {
                PackageSpecificWarningProperties = packageSpecificWarningProperties
            };

            var nonSuppressedMessage = RestoreLogMessage.CreateWarning(NuGetLogCode.NU1500, "Warning", libraryId);

            // Act && Assert
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(nonSuppressedMessage));
            Assert.Equal(LogLevel.Warning, nonSuppressedMessage.Level);
        }

        [Fact]
        public void WarningPropertiesCollection_PackagePropertiesWithoutFrameworkAndWarningWithFramework()
        {
            // Arrange
            // Arrange
            var libraryId = "test_library";
            var frameworkString = "net45";

            var packageSpecificWarningProperties = new PackageSpecificWarningProperties();
            packageSpecificWarningProperties.Add(NuGetLogCode.NU1500, libraryId);

            var warningPropertiesCollection = new WarningPropertiesCollection()
            {
                PackageSpecificWarningProperties = packageSpecificWarningProperties
            };

            var nonSuppressedMessage = RestoreLogMessage.CreateWarning(NuGetLogCode.NU1500, "Warning", libraryId, frameworkString);

            // Act && Assert
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(nonSuppressedMessage));
            Assert.Equal(LogLevel.Warning, nonSuppressedMessage.Level);
        }

        [Fact]
        public void WarningPropertiesCollection_PackagePropertiesWithoutFrameworkAndWarningWithoutFramework()
        {
            // Arrange
            // Arrange
            var libraryId = "test_library";

            var packageSpecificWarningProperties = new PackageSpecificWarningProperties();
            packageSpecificWarningProperties.Add(NuGetLogCode.NU1500, libraryId);

            var warningPropertiesCollection = new WarningPropertiesCollection()
            {
                PackageSpecificWarningProperties = packageSpecificWarningProperties
            };

            var suppressedMessage = RestoreLogMessage.CreateWarning(NuGetLogCode.NU1500, "Warning", libraryId);

            // Act && Assert
            Assert.True(warningPropertiesCollection.ApplyWarningProperties(suppressedMessage));
        }

        [Fact]
        public void WarningPropertiesCollection_PackagePropertiesWithNoWarnAndProjectProperties()
        {
            // Arrange
            // Arrange
            var libraryId = "test_library";
            var frameworkString = "net45";
            var targetFramework = NuGetFramework.Parse(frameworkString);
            var noWarnSet = new HashSet<NuGetLogCode> { };
            var warnAsErrorSet = new HashSet<NuGetLogCode> { };
            var allWarningsAsErrors = false;

            var packageSpecificWarningProperties = new PackageSpecificWarningProperties();
            packageSpecificWarningProperties.Add(NuGetLogCode.NU1500, libraryId, targetFramework);

            var warningPropertiesCollection = new WarningPropertiesCollection()
            {
                ProjectWideWarningProperties = new WarningProperties(warnAsErrorSet, noWarnSet, allWarningsAsErrors),
                PackageSpecificWarningProperties = packageSpecificWarningProperties
            };

            var suppressedMessage = RestoreLogMessage.CreateWarning(NuGetLogCode.NU1500, "Warning", libraryId, frameworkString);

            // Act && Assert
            Assert.True(warningPropertiesCollection.ApplyWarningProperties(suppressedMessage));
        }

        [Fact]
        public void WarningPropertiesCollection_PackagePropertiesAndProjectPropertiesWithNoWarn()
        {
            // Arrange
            // Arrange
            var libraryId = "test_library";
            var frameworkString = "net45";
            var targetFramework = NuGetFramework.Parse(frameworkString);
            var noWarnSet = new HashSet<NuGetLogCode> { NuGetLogCode.NU1500 };
            var warnAsErrorSet = new HashSet<NuGetLogCode> { };
            var allWarningsAsErrors = false;

            var packageSpecificWarningProperties = new PackageSpecificWarningProperties();

            var warningPropertiesCollection = new WarningPropertiesCollection()
            {
                ProjectWideWarningProperties = new WarningProperties(warnAsErrorSet, noWarnSet, allWarningsAsErrors),
                PackageSpecificWarningProperties = packageSpecificWarningProperties
            };

            var suppressedMessage = RestoreLogMessage.CreateWarning(NuGetLogCode.NU1500, "Warning", libraryId);

            // Act && Assert
            Assert.True(warningPropertiesCollection.ApplyWarningProperties(suppressedMessage));
        }

        [Fact]
        public void WarningPropertiesCollection_PackagePropertiesWithNoWarnAndProjectPropertiesWithWarnAsError()
        {
            // Arrange
            // Arrange
            var libraryId = "test_library";
            var frameworkString = "net45";
            var targetFramework = NuGetFramework.Parse(frameworkString);
            var noWarnSet = new HashSet<NuGetLogCode> { };
            var warnAsErrorSet = new HashSet<NuGetLogCode> { NuGetLogCode.NU1500 };
            var allWarningsAsErrors = false;

            var packageSpecificWarningProperties = new PackageSpecificWarningProperties();
            packageSpecificWarningProperties.Add(NuGetLogCode.NU1500, libraryId, targetFramework);

            var warningPropertiesCollection = new WarningPropertiesCollection()
            {
                ProjectWideWarningProperties = new WarningProperties(warnAsErrorSet, noWarnSet, allWarningsAsErrors),
                PackageSpecificWarningProperties = packageSpecificWarningProperties
            };

            var suppressedMessage = RestoreLogMessage.CreateWarning(NuGetLogCode.NU1500, "Warning", libraryId, frameworkString);
            var upgradedMessage = RestoreLogMessage.CreateWarning(NuGetLogCode.NU1500, "Warning", libraryId);
            var unaffectedMessage = RestoreLogMessage.CreateWarning(NuGetLogCode.NU1601, "Warning", libraryId);

            // Act && Assert
            Assert.True(warningPropertiesCollection.ApplyWarningProperties(suppressedMessage));
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(upgradedMessage));
            Assert.Equal(LogLevel.Error, upgradedMessage.Level);
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(unaffectedMessage));
            Assert.Equal(LogLevel.Warning, unaffectedMessage.Level);
        }

        [Fact]
        public void WarningPropertiesCollection_PackagePropertiesWithNoWarnAndProjectPropertiesWithAllWarnAsError()
        {
            // Arrange
            // Arrange
            var libraryId = "test_library";
            var frameworkString = "net45";
            var targetFramework = NuGetFramework.Parse(frameworkString);
            var noWarnSet = new HashSet<NuGetLogCode> { };
            var warnAsErrorSet = new HashSet<NuGetLogCode> { };
            var allWarningsAsErrors = true;

            var packageSpecificWarningProperties = new PackageSpecificWarningProperties();
            packageSpecificWarningProperties.Add(NuGetLogCode.NU1500, libraryId, targetFramework);

            var warningPropertiesCollection = new WarningPropertiesCollection()
            {
                ProjectWideWarningProperties = new WarningProperties(warnAsErrorSet, noWarnSet, allWarningsAsErrors),
                PackageSpecificWarningProperties = packageSpecificWarningProperties
            };

            var suppressedMessage = RestoreLogMessage.CreateWarning(NuGetLogCode.NU1500, "Warning", libraryId, frameworkString);
            var upgradedMessage = RestoreLogMessage.CreateWarning(NuGetLogCode.NU1500, "Warning", libraryId);

            // Act && Assert
            Assert.True(warningPropertiesCollection.ApplyWarningProperties(suppressedMessage));
            Assert.False(warningPropertiesCollection.ApplyWarningProperties(upgradedMessage));
            Assert.Equal(LogLevel.Error, upgradedMessage.Level);
        }
    }
}
