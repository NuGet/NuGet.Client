// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.Packaging.Rules;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class UpholdBuildConventionRuleTests
    {
        public static readonly List<object[]> WarningNotRaisedData
            = new List<object[]>
            {
                    new object[] { new string[] { "build/packageId.props" } },
                    new object[] { new string[] { "build/net45/packageId.props" } },
                    new object[] { new string[] { "build/net45/packageId.targets" } },
                    new object[] { new string[] { "build/packageId.targets" } },
                    new object[] { new string[] { "build/net45/packageId.props", "build/net45/packageId.targets" } },
                    new object[] { new string[] { "build/packageId.props", "build/packageId.targets" } },
                    new object[] { new string[] { "build/PackageID.props", "build/PackageID.targets" } },
                    new object[] { new string[] { "build/extra/packageId.props", "build/packageId.props" } },
                    new object[] { new string[] { "build/net45/extra/packageId.props", "build/net45/packageId.props" } },
                    new object[] { new string[] { "build/random.dll" } },
                    new object[] { new string[] { "buildCrossTargeting/packageId.props", "buildCrossTargeting/packageId.targets" } },
                    new object[] { new string[] { "buildCrossTargeting/net45/packageId.props", "buildCrossTargeting/net45/packageId.targets" } },
                    new object[] { new string[] { "buildTransitive/packageId.props", "buildTransitive/packageId.targets" } },
                    new object[] { new string[] { "buildTransitive/net45/packageId.props", "buildTransitive/net45/packageId.targets" } },
                    new object[] { new string[] { @"build\packageId.props" } },
            };

        [Theory]
        [MemberData(nameof(WarningNotRaisedData))]
        public void IdentifyViolators_PackageWithCorrectlyNamedMSBuildFile_DoesNotFindViolations(string[] files)
        {
            //Arrange
            string packageId = "packageId";

            //Act
            var rule = new UpholdBuildConventionRule();
            var actual = rule.IdentifyViolators(files, packageId);

            //Assert
            Assert.Empty(actual);
        }

        public static readonly List<object[]> WarningRaisedData
            = new List<object[]>
            {
                    new object[] { new string[] { "build/other.props" } },
                    new object[] { new string[] { "build/net45/other.props" } },
                    new object[] { new string[] { "build/net45/other.targets" } },
                    new object[] { new string[] { "build/other.targets" } },
                    new object[] { new string[] { "build/extra/other.props" } },
                    new object[] { new string[] { "build/net45/extra/other.props" } },
                    new object[] { new string[] { "buildCrossTargeting/net45/other.props"} },
                    new object[] { new string[] { "buildCrossTargeting/net45/extra/other.props"} },
                    new object[] { new string[] { "buildCrossTargeting/other.props"} },
                    new object[] { new string[] { "buildCrossTargeting/extra/other.props"} },
                    new object[] { new string[] { "buildTransitive/net45/other.props"} },
                    new object[] { new string[] { "buildTransitive/net45/extra/other.props"} },
                    new object[] { new string[] { "buildTransitive/other.props"} },
                    new object[] { new string[] { "buildTransitive/extra/other.props"} },
                    new object[] { new string[] { @"build\other.props" } }
            };

        [Theory]
        [MemberData(nameof(WarningRaisedData))]
        public void IdentifyViolators_PackageWithIncorrectlyNamedMSBuildFile_FindsViolations(string[] files)
        {
            //Arrange
            string packageId = "packageId";

            //Act
            var rule = new UpholdBuildConventionRule();
            var issues = rule.IdentifyViolators(files, packageId);

            //Assert
            Assert.Single(issues);
        }

        [Fact]
        public void IdentifyViolators_PackageWithFileNameSimilarToBuildDirectory_DoesNotWarn()
        {
            // Arrange
            var packageId = "PackageId";
            var files = new[]
            {
                @"buildCustom\anything.props",
                "buildCustom/anything.targets"
            };

            // Act
            var target = new UpholdBuildConventionRule();
            var actual = target.IdentifyViolators(files, packageId);

            // Assert
            Assert.Empty(actual);
        }

        [Fact]
        public void IdentifyViolators_NonCompliantFileInBuildRoot_ExpectedPathIsBuildRoot()
        {
            // Arrange
            var files = new[]
            {
                "build/other.props"
            };
            var packageId = "packageId";

            // Act
            var target = new UpholdBuildConventionRule();
            var actual = target.IdentifyViolators(files, packageId);

            // Assert
            var violation = Assert.Single(actual);
            Assert.Equal("build/", violation.Path);
            Assert.Equal("build/packageId.props", violation.ExpectedPath);
        }

        [Fact]
        public void IdentifyViolators_NonCompliantFileInNonTfmPath_ExpectedPathIsBuildRoot()
        {
            // Arrange
            var files = new[]
            {
                "build/custom/other.props"
            };
            var packageId = "packageId";

            // Act
            var target = new UpholdBuildConventionRule();
            var actual = target.IdentifyViolators(files, packageId);

            // Assert
            var violation = Assert.Single(actual);
            Assert.Equal("build/", violation.Path);
            Assert.Equal("build/packageId.props", violation.ExpectedPath);
        }

        [Fact]
        public void IdentifyViolators_NonCompliantFileInTfmPath_ExpectedPathIsBuildRoot()
        {
            // Arrange
            var files = new[]
            {
                "build/net5.0/other.props"
            };
            var packageId = "packageId";

            // Act
            var target = new UpholdBuildConventionRule();
            var actual = target.IdentifyViolators(files, packageId);

            // Assert
            var violation = Assert.Single(actual);
            Assert.Equal("build/net5.0/", violation.Path);
            Assert.Equal("build/net5.0/packageId.props", violation.ExpectedPath);
        }

        [Fact]
        public void IdentifyViolators_NonCompliantFileInTfmSubDirectory_ExpectedPathIsBuildRoot()
        {
            // Arrange
            var files = new[]
            {
                "build/net5.0/custom/other.props"
            };
            var packageId = "packageId";

            // Act
            var target = new UpholdBuildConventionRule();
            var actual = target.IdentifyViolators(files, packageId);

            // Assert
            var violation = Assert.Single(actual);
            Assert.Equal("build/net5.0/", violation.Path);
            Assert.Equal("build/net5.0/packageId.props", violation.ExpectedPath);
        }

        [Fact]
        public void IdentifyViolators_MultiplePropsInOneDirectory_GeneratesOneViolation()
        {
            // Arrange
            var files = new[]
            {
                "build/one.props",
                "build/two.props"
            };
            var packageId = "PackageId";

            // Act
            var target = new UpholdBuildConventionRule();
            var actual = target.IdentifyViolators(files, packageId);

            // Assert
            Assert.Equal(1, actual.Count);
        }

        [Theory]
        [InlineData("build/")]
        [InlineData("build/net5.0/")]
        public void IdentifyViolators_MultiplePropsInSubDirectories_GeneratesOneViolation(string pathToTest)
        {
            // Arrange
            var files = new[]
            {
                pathToTest + "one.props",
                pathToTest + "two/two.props",
                pathToTest + "three/three.props"
            };
            var packageId = "PackageId";

            // Act
            var target = new UpholdBuildConventionRule();
            var actual = target.IdentifyViolators(files, packageId);

            // Assert
            Assert.Equal(1, actual.Count);
        }

        [Fact]
        public void IdentifyViolators_DifferentPathSeparators_GroupTogether()
        {
            // Arrange
            var files = new[]
            {
                "build/net5.0/one.props",
                @"build\net5.0\two.props"
            };
            var packageId = "PackageId";

            // Act
            var target = new UpholdBuildConventionRule();
            var actual = target.IdentifyViolators(files, packageId);

            // Assert
            Assert.Equal(1, actual.Count);
        }

        [Fact]
        public void GenerateWarnings_SingleIssue_SingleLineMessage()
        {
            //Arrange
            var issues = new[]
            {
                new UpholdBuildConventionRule.ConventionViolator("build/net45/", ".props", "build/net45/packageId.props")
            };

            //Act
            var target = new UpholdBuildConventionRule();
            var warning = target.GenerateWarning(issues);

            //Assert
            Assert.NotNull(warning);
            Assert.Equal(NuGetLogCode.NU5129, warning.Code);
            var expectedMessage = "- At least one .props file was found in 'build/net45/', but 'build/net45/packageId.props' was not." + Environment.NewLine;

            Assert.Equal(expectedMessage, warning.Message);
        }

        [Fact]
        public void GenerateWarnings_MultipleIssues_MultiLineMessage()
        {
            //Arrange
            var issues = new List<UpholdBuildConventionRule.ConventionViolator>
            {
                new UpholdBuildConventionRule.ConventionViolator("build/net45/", ".props", "build/net45/packageId.props"),
                new UpholdBuildConventionRule.ConventionViolator("build/net45/", ".targets", "build/net45/packageId.targets")
            };

            issues.Sort(UpholdBuildConventionRule.ConventionViolatorComparer.Instance);

            //Act
            var target = new UpholdBuildConventionRule();
            var warning = target.GenerateWarning(issues);

            //Assert
            Assert.NotNull(warning);
            Assert.Equal(NuGetLogCode.NU5129, warning.Code);
            var expectedMessage = "- At least one .props file was found in 'build/net45/', but 'build/net45/packageId.props' was not." + Environment.NewLine
                + "- At least one .targets file was found in 'build/net45/', but 'build/net45/packageId.targets' was not." + Environment.NewLine;

            Assert.Equal(expectedMessage, warning.Message);
        }
    }
}
