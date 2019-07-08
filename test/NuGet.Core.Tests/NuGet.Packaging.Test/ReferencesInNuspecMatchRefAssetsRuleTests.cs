using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Castle.DynamicProxy.Generators.Emitters.SimpleAST;
using NuGet.Common;
using NuGet.Packaging.Rules;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class ReferencesInNuspecMatchRefAssetsRuleTests
    {
        [Fact]
        public void Compare_PackageWithReferencesAndRefFolderMatching_ShouldBeEmpty()
        {
            //Arrange
            var references = new Dictionary<string, string[]>
            {
                {"net462", new string[] {"MyLib.dll", "MyHelpers.dll"} }
            };
            var files = new string[]
            {
                "ref/net462/MyLib.dll",
                "ref/net462/MyHelpers.dll"
            };

            //Act
            var rule = new ReferencesInNuspecMatchRefAssetsRule();
            var missingItems = rule.Compare(references, files);

            //Assert
            Assert.Empty(missingItems);
        }

        [Fact]
        public void Compare_PackageWithReferencesThatTheRefFolderDoesNotHave_RefFilesMissingShouldNotBeEmpty()
        {
            //Arrange
            var references = new Dictionary<string, string[]>
            {
                {"net462", new string[] {"MyLib.dll", "MyHelpers.dll"} }
            };
            var files = new string[]
            {
                "ref/net462/MyLib.dll",
            };

            //Act
            var rule = new ReferencesInNuspecMatchRefAssetsRule();
            var missingItems = rule.Compare(references, files);

            //Assert
            Assert.Equal(missingItems.Count(), 1);
            var singleIssue = missingItems.Single(t => t.TFM.Equals("net462") && t.MissingItems.Contains("MyHelpers.dll"));
        }

        [Fact]
        public void Compare_PackageWithRefAssetsThatTheNuspecDoesNotHave_ShouldHaveOneMissingItem()
        {
            //Arrange
            var references = new Dictionary<string, string[]>
            {
                {"net462", new string[] {"MyLib.dll"} }
            };
            var files = new string[]
            {
                "ref/net462/MyLib.dll",
                "ref/net462/MyHelpers.dll"
            };

            //Act
            var rule = new ReferencesInNuspecMatchRefAssetsRule();
            var missingItems = rule.Compare(references, files);

            //Assert
            Assert.Equal(missingItems.Count(), 1);
            var singleIssue = missingItems.Single(t => t.TFM.Equals("net462") && t.MissingItems.Contains("MyHelpers.dll"));
        }

        [Fact]
        public void Compare_PackageWithRefAssetsUnderRefFolder_ShouldBeEmpty()
        {
            //Arrange
            var references = new Dictionary<string, string[]>();
            var files = new string[]
            {
                "ref/MyLib.dll",
                "ref/MyHelpers.dll"
            };

            //Act
            var rule = new ReferencesInNuspecMatchRefAssetsRule();
            var missingItems = rule.Compare(references, files);

            //Assert
            Assert.Empty(missingItems);
        }

        [Fact]
        public void Compare_PackageWithAssetsUnderMiscellaneousFolders_ShouldBeEmpty()
        {
            //Arrange
            var references = new Dictionary<string, string[]>();
            var files = new string[]
            {
                "ref/MyCompany/MyLib.dll",
                "ref/MyCompany/MyHelpers.dll"
            };

            //Act
            var rule = new ReferencesInNuspecMatchRefAssetsRule();
            var missingItems = rule.Compare(references, files);

            //Assert
            Assert.Empty(missingItems);
        }

        [Fact]
        public void Compare_PackageWithMismatchedReferencesInMultipleSubFolders_ShouldHaveCorrectItems()
        {
            //Arrange
            var references = new Dictionary<string, string[]>
            {
                {"net462", new string[] {"MyLib.dll", "MyHelpers.dll"} },
                {"net472", new string[] { "MyLib.dll"} }
            };
            var files = new string[]
            {
                "ref/net462/MyLib.dll",
                "ref/net472/MyLib.dll",
                "ref/net472/MyHelpers.dll"
            };

            //Act
            var rule = new ReferencesInNuspecMatchRefAssetsRule();
            var missingItems = rule.Compare(references, files);

            //Assert
            Assert.Equal(missingItems.Count(), 2);
            var singleRefFilesMissing = missingItems.Single(t => t.TFM == "net462" && t.MissingItems.Count() == 1 && t.MissingItems.Contains("MyHelpers.dll"));
            var singleReferencesMissing = missingItems.Single(t => t.TFM == "net472" && t.MissingItems.Count() == 1 && t.MissingItems.Contains("MyHelpers.dll"));
        }

        [Fact]
        public void GenerateWarnings_PackageWithMatchingAssetsAndReferences_ShouldNotWarn()
        {
            //Arrange
            var references = new Dictionary<string, string[]>
            {
                {"net462", new string[] {"MyLib.dll", "MyHelpers.dll"} }
            };
            var files = new string[]
            {
                "ref/net462/MyLib.dll",
                "ref/net462/MyHelpers.dll"
            };

            //Act
            var rule = new ReferencesInNuspecMatchRefAssetsRule();
            var missingItems = rule.Compare(references, files);
            var issues = rule.GenerateWarnings(missingItems);

            //Assert
            Assert.Empty(issues);
        }

        [Fact]
        public void GenerateWarnings_PackageWithReferencesMissingFromTheNuspec_ShouldWarn()
        {
            //Arrange
            var references = new Dictionary<string, string[]>
            {
                {"net462", new string[] {"MyLib.dll"} }
            };
            var files = new string[]
            {
                "ref/net462/MyLib.dll",
                "ref/net462/MyHelpers.dll"
            };

            //Act
            var rule = new ReferencesInNuspecMatchRefAssetsRule();
            var missingItems = rule.Compare(references, files);
            var issues = rule.GenerateWarnings(missingItems);

            //Assert
            Assert.Equal(issues.Count(), 1);
            var singleIssue = issues.Single(t => t.Code == NuGetLogCode.NU5131);
            var expectedMessage = "References were found in the nuspec, but some were not found within the ref folder. Add the following reference assemblies:" + Environment.NewLine +
                "- Add MyHelpers.dll to the net462 reference group in the nuspec" + Environment.NewLine;
            Assert.Equal(singleIssue.Message, expectedMessage);
        }

        [Fact]
        public void GenerateWarnings_PackageWithAssetsMissingFromTheRefFolder_ShouldWarn()
        {
            //Arrange
            var references = new Dictionary<string, string[]>
            {
                {"net462", new string[] { "MyLib.dll", "MyHelpers.dll"} }
            };
            var files = new string[]
            {
                "ref/net462/MyLib.dll"
            };

            //Act
            var rule = new ReferencesInNuspecMatchRefAssetsRule();
            var missingItems = rule.Compare(references, files);
            var issues = rule.GenerateWarnings(missingItems);

            //Assert
            Assert.Equal(issues.Count(), 1);
            var singleIssue = issues.Single(t => t.Code == NuGetLogCode.NU5131);
            var expectedMessage = "References were found in the nuspec, but some were not found within the ref folder. Add the following reference assemblies:" + Environment.NewLine +
                "- Add MyHelpers.dll to the ref/net462/ directory" + Environment.NewLine;
            Assert.Equal(singleIssue.Message, expectedMessage);
        }

        [Fact]
        public void GenerateWarnings_PackageWithSubfoldersThatAreMismatched_ShouldWarn()
        {
            //Arrange
            var references = new Dictionary<string, string[]>
            {
                {"net462", new string[] { "MyLib.dll", } },
                {"net472", new string[] { "MyLib.dll", "MyHelpers.dll"} }
            };
            var files = new string[]
            {
                "ref/net462/MyLib.dll",
                "ref/net462/MyHelpers.dll",
                "ref/net472/MyHelpers.dll"
            };

            //Act
            var rule = new ReferencesInNuspecMatchRefAssetsRule();
            var missingItems = rule.Compare(references, files);
            var issues = rule.GenerateWarnings(missingItems);

            //Assert
            Assert.Equal(1 ,issues.Count());
            var singleIssue = issues.Single(t => t.Code == NuGetLogCode.NU5131);
            var expectedMessage = "References were found in the nuspec, but some were not found within the ref folder. Add the following reference assemblies:" + Environment.NewLine +
                "- Add MyLib.dll to the ref/net472/ directory" + Environment.NewLine +
                "- Add MyHelpers.dll to the net462 reference group in the nuspec" + Environment.NewLine;
            Assert.Equal(singleIssue.Message, expectedMessage);
        }

        [Fact]
        public void GenerateWarnings_PackageWithSubFoldersThatAreCorrect_ShouldNotWarn()
        {
            //Arrange
            var references = new Dictionary<string, string[]>
            {
                {"net462", new string[] { "MyLib.dll", "MyHelpers.dll"} },
                {"net472", new string[] { "MyLib.dll", "MyHelpers.dll"} }
            };
            var files = new string[]
            {
                "ref/net462/MyLib.dll",
                "ref/net462/MyHelpers.dll",
                "ref/net472/MyLib.dll",
                "ref/net472/MyHelpers.dll"
            };

            //Act
            var rule = new ReferencesInNuspecMatchRefAssetsRule();
            var missingItems = rule.Compare(references, files);
            var issues = rule.GenerateWarnings(missingItems);

            //Assert
            Assert.Empty(issues);
        }
    }
}
