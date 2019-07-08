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
            Assert.True(issues.All(p => p.Code == NuGetLogCode.NU5129));
        }

        [Fact]
        public void GenerateWarnings_PackageWithPropsAndTargetsInSameSubFolderDoesNotFollowConvention_ShouldThrowTwoWarnings()
        {
            //Arrange
            string packageId = "packageId";
            var files = new string[]
            {
                "build/net45/packageID.props",
                "build/net45/packageID.targets"
            };
            //Act
            var rule = new UpholdBuildConventionRule();
            var (propsViolators, targetsViolators) = rule.IdentifyViolators(files, packageId);
            var issues = rule.GenerateWarnings(propsViolators, targetsViolators);

            //Assert
            Assert.Equal(issues.Count(), 2);
            Assert.True(issues.All(p => p.Code == NuGetLogCode.NU5129));
        }

        [Fact]
        public void GenerateWarnings_PackageWithPropsAndTargetsInMultipleSubFolders_ShouldThrowWarningForEachFolder()
        {
            //Arrange
            string packageId = "packageId";
            var files = new string[]
            {
                "build/net45/packageID.props",
                "build/net462/packageID.props",
                "build/net471/packageID.props",
                "build/netstandard1.3/packageID.props",
                "build/netcoreapp1.1/packageID.props"

            };
            var tfms = files.Select(t => t.Split('/')[1]);
            //Act
            var rule = new UpholdBuildConventionRule();
            var (propsViolators, targetsViolators) = rule.IdentifyViolators(files, packageId);
            var issues = rule.GenerateWarnings(propsViolators, targetsViolators);

            //Assert
            Assert.Equal(issues.Count(), 5);
            Assert.True(issues.All(p => p.Code == NuGetLogCode.NU5129));
            foreach(var tfm in tfms)
            {
                var message = string.Format("A .props file was found in 'build/{0}'. However, this file is not 'packageId.props'. Change the file to fit this format.", tfm);
                Assert.True(issues.Any(p => p.Message.Equals(message)));
            }
        }

        [Fact]
        public void GenerateWarnings_PackageWithPropsAndTargetsUnderBuild_ShouldThrowWarningTwice()
        {
            //Arrange
            string packageId = "packageId";
            var files = new string[]
            {
                "build/packageID.props",
                "build/packageID.targets"

            };

            //Act
            var rule = new UpholdBuildConventionRule();
            var (propsViolators, targetsViolators) = rule.IdentifyViolators(files, packageId);
            var issues = rule.GenerateWarnings(propsViolators, targetsViolators);

            //Assert
            Assert.Equal(issues.Count(), 2);
            Assert.True(issues.All(p => p.Code == NuGetLogCode.NU5129));
            var propsIssue = issues.Single(p => p.Message.Equals("A .props file was found in 'build'. However, this file is not 'packageId.props'. Change the file to fit this format."));
            var targetsIssue = issues.Single(p => p.Message.Equals("A .targets file was found in 'build'. However, this file is not 'packageId.targets'. Change the file to fit this format."));
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
                "build/net462/packageID.props",
                "build/net471/packageID.props",
                "build/netstandard1.3/packageId.targets",
                "build/netstandard1.3/packageID.targets",
                "build/netcoreapp1.1/packageID.props",
                "build/packageId.props",
                "build/package_ID.props"
            };

            //Act
            var rule = new UpholdBuildConventionRule();
            var (propsViolators, targetsViolators) = rule.IdentifyViolators(files, packageId);

            //Assert
            Assert.Equal(propsViolators.Count(), 3);
            Assert.Empty(targetsViolators);
            Assert.False(propsViolators.Keys.Contains("build/net45"));
            Assert.False(targetsViolators.Keys.Contains("build/netstandard1.3"));
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
                "build/packageID.targets"
            };

            //Act
            var rule = new UpholdBuildConventionRule();
            var (propsViolators, targetsViolators) = rule.IdentifyViolators(files, packageId);

            //Assert
            Assert.Empty(propsViolators);
            Assert.Equal(targetsViolators.Count(), 1);
            Assert.True(targetsViolators.Keys.Contains("build"));
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
                    new object[] { new string[] { "build/packageID.props" } },
                    new object[] { new string[] { "build/net45/packageID.props" } },
                    new object[] { new string[] { "build/net45/packageID.targets" } },
                    new object[] { new string[] { "build/packageID.targets" } },
                };
        }
    }
}
