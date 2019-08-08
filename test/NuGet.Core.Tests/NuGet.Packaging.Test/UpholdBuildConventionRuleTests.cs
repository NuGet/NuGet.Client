// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Castle.DynamicProxy.Generators.Emitters;
using NuGet.Common;
using NuGet.Packaging.Rules;
using Org.BouncyCastle.Asn1.X509;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class UpholdBuildConventionRuleTests
    {
        [Theory]
        [MemberData("WarningNotRaisedData", MemberType = typeof(FileSource))]
        public void WarningNotRaisedWhenGenerateWarningsCalledAndConventionIsFollowed(string[] files)
        {
            //Arrange
            string packageId = "packageId";

            //Act
            var rule = new UpholdBuildConventionRule();
            var conventionViolators = rule.IdentifyViolators(files, packageId);
            var issues = rule.GenerateWarnings(conventionViolators);

            //Assert
            Assert.Empty(issues);
        }

        [Theory]
        [MemberData("WarningRaisedData", MemberType = typeof(FileSource))]
        public void WarningRaisedWhenGenerateWarningsCalledAndConventionIsNotFollowed(string[] files)
        {
            //Arrange
            string packageId = "packageId";

            //Act
            var rule = new UpholdBuildConventionRule();
            var conventionViolators = rule.IdentifyViolators(files, packageId);
            var issues = rule.GenerateWarnings(conventionViolators);

            //Assert
            Assert.Equal(issues.Count(), 1);
            var singleIssue = issues.Single(p => p.Code == NuGetLogCode.NU5129);
        }

        public static class FileSource
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
                    new object[] { new string[] { "buildTransitive/net45/packageId.props", "buildTransitive/net45/packageId.targets" } }
                };

            public static readonly List<object[]> WarningRaisedData
                = new List<object[]>
                {
                    new object[] { new string[] { "build/package_Id.props" } },
                    new object[] { new string[] { "build/net45/package_Id.props" } },
                    new object[] { new string[] { "build/net45/package_Id.targets" } },
                    new object[] { new string[] { "build/package_Id.targets" } },
                    new object[] { new string[] { "build/extra/packageId.props" } },
                    new object[] { new string[] { "build/net45/extra/packageId.props" } },
                    new object[] { new string[] { "buildCrossTargeting/net45/package_Id.props"} },
                    new object[] { new string[] { "buildCrossTargeting/net45/extra/packageId.props"} },
                    new object[] { new string[] { "buildCrossTargeting/package_Id.props"} },
                    new object[] { new string[] { "buildCrossTargeting/extra/package_Id.props"} },
                    new object[] { new string[] { "buildTransitive/net45/package_Id.props"} },
                    new object[] { new string[] { "buildTransitive/net45/extra/packageId.props"} },
                    new object[] { new string[] { "buildTransitive/package_Id.props"} },
                    new object[] { new string[] { "buildTransitive/extra/package_Id.props"} }
                };
        }

        [Fact]
        public void GenerateWarnings_PackageWithPropsAndTargetsInSameSubFolderDoesNotFollowConvention_ShouldWarn()
        {
            //Arrange
            string packageId = "packageId";
            var files = new string[]
            {
                "build/net45/package_Id.props",
                "build/net45/package_Id.targets"
            };
            //Act
            var rule = new UpholdBuildConventionRule();
            var conventionViolators = rule.IdentifyViolators(files, packageId);
            var issues = rule.GenerateWarnings(conventionViolators);

            //Assert
            Assert.Equal(issues.Count(), 1);
            var singleIssue = issues.Single(p => p.Code == NuGetLogCode.NU5129);
            var expectedMessage = "- At least one .targets file was found in 'build/net45/', but 'build/net45/packageId.targets' was not." + Environment.NewLine +
                "- At least one .props file was found in 'build/net45/', but 'build/net45/packageId.props' was not." + Environment.NewLine;
            Assert.True(singleIssue.Message.Equals(expectedMessage));
        }

        [Fact]
        public void GenerateWarnings_PackageWithPropsAndTargetsInMultipleSubFolders_ShouldWarn()
        {
            //Arrange
            string packageId = "packageId";
            var files = new string[]
            {
                "build/net45/package_Id.props",
                "build/net462/package_Id.props",
                "build/net471/package_Id.props",
                "build/netstandard1.3/package_Id.props",
                "build/netcoreapp1.1/package_Id.props"

            };

            //Act
            var rule = new UpholdBuildConventionRule();
            var conventionViolators = rule.IdentifyViolators(files, packageId);
            var issues = rule.GenerateWarnings(conventionViolators);

            //Assert
            Assert.Equal(issues.Count(), 1);
            var singleIssue = issues.Single(p => p.Code == NuGetLogCode.NU5129);
            var expectedMessage = "- At least one .props file was found in 'build/net45/', but 'build/net45/packageId.props' was not." + Environment.NewLine +
                "- At least one .props file was found in 'build/net462/', but 'build/net462/packageId.props' was not." + Environment.NewLine +
                "- At least one .props file was found in 'build/net471/', but 'build/net471/packageId.props' was not." + Environment.NewLine +
                "- At least one .props file was found in 'build/netstandard1.3/', but 'build/netstandard1.3/packageId.props' was not." + Environment.NewLine +
                "- At least one .props file was found in 'build/netcoreapp1.1/', but 'build/netcoreapp1.1/packageId.props' was not." + Environment.NewLine;
            Assert.True(singleIssue.Message.Equals(expectedMessage));
        }

        [Fact]
        public void GenerateWarnings_PackageWithPropsAndTargetsUnderBuild_ShouldWarn()
        {
            //Arrange
            string packageId = "packageId";
            var files = new string[]
            {
                "build/package_Id.props",
                "build/package_Id.targets"
            };

            //Act
            var rule = new UpholdBuildConventionRule();
            var conventionViolators = rule.IdentifyViolators(files, packageId);
            var issues = rule.GenerateWarnings(conventionViolators);

            //Assert
            Assert.Equal(issues.Count(), 1);
            var singleIssue = issues.Single(p => p.Code == NuGetLogCode.NU5129);
            var expectedMessage = "- At least one .targets file was found in 'build/', but 'build/packageId.targets' was not." + Environment.NewLine +
                "- At least one .props file was found in 'build/', but 'build/packageId.props' was not." + Environment.NewLine;
            Assert.True(singleIssue.Message.Equals(expectedMessage));
        }

        [Fact]
        public void IdentifyViolators_PackageWithPropsAndTargetsFilesInSubfolders_ShouldHaveTheCorrectLocations()
        {
            //Arrange
            string packageId = "packageId";
            var files = new string[]
            {
                "build/net45/packageId.props",
                "build/net45/package_ID.props",
                "build/net462/package_Id.props",
                "build/netstandard1.3/packageId.targets",
                "build/netstandard1.3/package_Id.targets",
                "build/netcoreapp1.1/package_Id.props",
                "build/packageId.props",
                "build/package_ID.props"
            };

            //Act
            var rule = new UpholdBuildConventionRule();
            var conventionViolators = rule.IdentifyViolators(files, packageId);

            //Assert
            Assert.Equal(2, conventionViolators.Count());
            Assert.False(conventionViolators.Any(t => t.Path.Equals("build/net45/")));
            Assert.False(conventionViolators.Any(t => t.Path.Equals("build/netstandard1.3/")));
            Assert.False(conventionViolators.Any(t => t.Path.Equals("build/")));
            var secondIssue = conventionViolators.Single(t => t.Path.Equals("build/netcoreapp1.1/"));
            var thirdIssue = conventionViolators.Single(t => t.Path.Equals("build/net462/"));
        }

        [Fact]
        public void IdentifyViolators_PackageWithPropsAndTargetsFilesUnderBuild_ShouldHaveTheCorrectLocations()
        {
            //Arrange
            string packageId = "packageId";
            var files = new string[]
            {
                "build/packageId.props",
                "build/package_ID.props",
                "build/package_Id.targets"
            };

            //Act
            var rule = new UpholdBuildConventionRule();
            var conventionViolators = rule.IdentifyViolators(files, packageId);

            //Assert
            Assert.Equal(conventionViolators.Count(), 1);
            Assert.True(conventionViolators.All(t => t.Path.Equals("build/")));
        }

        [Fact]
        public void GenerateWarnings_PackageHasInvalidBuildAndValidBuildTransitiveAndValidBuildCross_ShouldWarnOnce()
        {
            //Arrange
            string packageId = "packageId";
            var files = new string[]
            {
                "build/package_Id.props",
                "buildTransitive/packageId.props",
                "buildCrossTargeting/packageId.props"
            };

            //Act
            var rule = new UpholdBuildConventionRule();
            var conventionViolators = rule.IdentifyViolators(files, packageId);
            var issues = rule.GenerateWarnings(conventionViolators);

            //Assert
            Assert.Equal(issues.Count(), 1);
            var singleIssue = issues.Single(p => p.Code == NuGetLogCode.NU5129);
            var expectedMessage = "- At least one .props file was found in 'build/', but 'build/packageId.props' was not." + Environment.NewLine;
            Assert.True(singleIssue.Message.Equals(expectedMessage));
        }

        [Fact]
        public void GenerateWarnings_PackageHasInvalidBuildAndInvalidBuildTransitiveAndValidBuildCross_ShouldWarnTwice()
        {
            //Arrange
            string packageId = "packageId";
            var files = new string[]
            {
                "build/package_Id.props",
                "buildTransitive/package_Id.props",
                "buildCrossTargeting/packageId.props"
            };

            //Act
            var rule = new UpholdBuildConventionRule();
            var conventionViolators = rule.IdentifyViolators(files, packageId);
            var issues = rule.GenerateWarnings(conventionViolators);

            //Assert
            Assert.Equal(issues.Count(), 2);
            var firstIssue = issues.Single(p => p.Code == NuGetLogCode.NU5129 && p.Message.Contains("build/packageId.props"));
            var firstExpectedMessage = "- At least one .props file was found in 'build/', but 'build/packageId.props' was not." + Environment.NewLine;
            Assert.True(firstIssue.Message.Equals(firstExpectedMessage));
            var secondIssue = issues.Single(p => p.Code == NuGetLogCode.NU5129 && p.Message.Contains("buildTransitive/packageId.props"));
            var secondExpectedMessage = "- At least one .props file was found in 'buildTransitive/', but 'buildTransitive/packageId.props' was not." + Environment.NewLine;
            Assert.True(secondIssue.Message.Equals(secondExpectedMessage));
        }

        [Fact]
        public void GenerateWarnings_PackageHasInvalidBuildAndInvalidBuildTransitiveAndInvalidBuildCross_ShouldWarnThrice()
        {
            //Arrange
            string packageId = "packageId";
            var files = new string[]
            {
                "build/package_Id.props",
                "buildTransitive/package_Id.props",
                "buildCrossTargeting/package_Id.props"
            };

            //Act
            var rule = new UpholdBuildConventionRule();
            var conventionViolators = rule.IdentifyViolators(files, packageId);
            var issues = rule.GenerateWarnings(conventionViolators);

            //Assert
            Assert.Equal(issues.Count(), 3);
            var firstIssue = issues.Single(p => p.Code == NuGetLogCode.NU5129 && p.Message.Contains("build/packageId.props"));
            var firstExpectedMessage = "- At least one .props file was found in 'build/', but 'build/packageId.props' was not." + Environment.NewLine;
            Assert.True(firstIssue.Message.Equals(firstExpectedMessage));
            var secondIssue = issues.Single(p => p.Code == NuGetLogCode.NU5129 && p.Message.Contains("buildTransitive/packageId.props"));
            var secondExpectedMessage = "- At least one .props file was found in 'buildTransitive/', but 'buildTransitive/packageId.props' was not." + Environment.NewLine;
            Assert.True(secondIssue.Message.Equals(secondExpectedMessage));
            var thirdIssue = issues.Single(p => p.Code == NuGetLogCode.NU5129 && p.Message.Contains("buildCrossTargeting/packageId.props"));
            var thirdExpectedMessage = "- At least one .props file was found in 'buildCrossTargeting/', but 'buildCrossTargeting/packageId.props' was not." + Environment.NewLine;
            Assert.True(thirdIssue.Message.Equals(thirdExpectedMessage));
        }
    }
}
