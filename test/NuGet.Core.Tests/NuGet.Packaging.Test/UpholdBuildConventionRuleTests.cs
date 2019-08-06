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
            var (propsViolators, targetsViolators) = rule.IdentifyViolators(files, packageId);
            var issues = rule.GenerateWarnings(propsViolators, targetsViolators);

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
            var (propsViolators, targetsViolators) = rule.IdentifyViolators(files, packageId);
            var issues = rule.GenerateWarnings(propsViolators, targetsViolators);

            //Assert
            Assert.Equal(issues.Count(), 1);
            var singleIssue = issues.Single(p => p.Code == NuGetLogCode.NU5129);
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
            var (propsViolators, targetsViolators) = rule.IdentifyViolators(files, packageId);
            var issues = rule.GenerateWarnings(propsViolators, targetsViolators);

            //Assert
            Assert.Equal(issues.Count(), 1);
            var singleIssue = issues.Single(p => p.Code == NuGetLogCode.NU5129);
            var expectedMessage = "- A .props file was found in the folder 'net45'. However, this file is not 'packageId.props'. Change the file to fit this format.\r\n" +
                "- A .targets file was found in the folder 'net45'. However, this file is not 'packageId.targets'. Change the file to fit this format.\r\n";
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
            var tfms = files.Select(t => t.Split('/')[1]);
            //Act
            var rule = new UpholdBuildConventionRule();
            var (propsViolators, targetsViolators) = rule.IdentifyViolators(files, packageId);
            var issues = rule.GenerateWarnings(propsViolators, targetsViolators);

            //Assert
            Assert.Equal(issues.Count(), 1);
            var singleIssue = issues.Single(p => p.Code == NuGetLogCode.NU5129);
            var expectedMessage = "- A .props file was found in the folder 'net45'. However, this file is not 'packageId.props'. Change the file to fit this format.\r\n" +
                "- A .props file was found in the folder 'net462'. However, this file is not 'packageId.props'. Change the file to fit this format.\r\n" +
                "- A .props file was found in the folder 'net471'. However, this file is not 'packageId.props'. Change the file to fit this format.\r\n" +
                "- A .props file was found in the folder 'netstandard1.3'. However, this file is not 'packageId.props'. Change the file to fit this format.\r\n" +
                "- A .props file was found in the folder 'netcoreapp1.1'. However, this file is not 'packageId.props'. Change the file to fit this format.\r\n";
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
            var (propsViolators, targetsViolators) = rule.IdentifyViolators(files, packageId);
            var issues = rule.GenerateWarnings(propsViolators, targetsViolators);

            //Assert
            Assert.Equal(issues.Count(), 1);
            var singleIssue = issues.Single(p => p.Code == NuGetLogCode.NU5129);
            var expectedMessage = "- A .props file was found in the folder 'build or other'. However, this file is not 'packageId.props'. Change the file to fit this format.\r\n" +
                "- A .targets file was found in the folder 'build or other'. However, this file is not 'packageId.targets'. Change the file to fit this format.\r\n";
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
                "build/net471/package_Id.props",
                "build/netstandard1.3/packageId.targets",
                "build/netstandard1.3/package_Id.targets",
                "build/netcoreapp1.1/package_Id.props",
                "build/packageId.props",
                "build/package_ID.props"
            };

            //Act
            var rule = new UpholdBuildConventionRule();
            var (propsViolators, targetsViolators) = rule.IdentifyViolators(files, packageId);

            //Assert
            Assert.Equal(propsViolators.Count(), 3);
            Assert.Empty(targetsViolators);
            Assert.False(propsViolators.Keys.Contains("net45"));
            Assert.False(targetsViolators.Keys.Contains("netstandard1.3"));
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
            var (propsViolators, targetsViolators) = rule.IdentifyViolators(files, packageId);

            //Assert
            Assert.Empty(propsViolators);
            Assert.Equal(targetsViolators.Count(), 1);
            Assert.True(targetsViolators.Keys.Contains("unsupported"));
        }

        [Fact]
        public void IdentifyViolators_PackageWithPropsAndTargetsIsCasedDifferentlyThanPackageId_ShouldNotWarn()
        {
            //Arrange
            string packageId = "packageId";
            var files = new string[]
            {
                "build/PackageID.props",
                "build/PackageID.targets"
            };

            //Act
            var rule = new UpholdBuildConventionRule();
            var (propsViolators, targetsViolators) = rule.IdentifyViolators(files, packageId);

            //Assert
            Assert.Empty(propsViolators);
            Assert.Empty(targetsViolators);
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
                    new object[] { new string[] { "build/packageId.props", "build/packageId.targets" } }
                };

            public static readonly List<object[]> WarningRaisedData
                = new List<object[]>
                {
                    new object[] { new string[] { "build/package_Id.props" } },
                    new object[] { new string[] { "build/net45/package_Id.props" } },
                    new object[] { new string[] { "build/net45/package_Id.targets" } },
                    new object[] { new string[] { "build/package_Id.targets" } },
                };
        }
    }
}
