using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Rules;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class DependeciesGroupsForEachTFMRuleTests
    {
        public static string NuspecContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
"<package xmlns=\"http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd\">" +
"   <metadata>" +
"        <id>test</id>" +
"        <version>1.0.0</version>" +
"        <authors>Unit Test</authors>" +
"        <description>Sample Description</description>" +
"        <language>en-US</language>" +
"    </metadata>" +
"</package>";



        [Fact]
        public void Validate_PackageWithDependenciesForEachTFMInLib_ShouldNotWarn()
        {
            var frameworks = new string[]
            {
                ".NETFramework2.0",
                ".NETFramework3.5",
                ".NETFramework4.0",
                ".NETFramework4.5",
                ".NETStandard1.0",
                ".NETStandard1.3",
                ".NETStandard2.0"
            };
            //Arrange
            var files = new string[]
            {
                "lib/net20/test.dll",
                "lib/net35/test.dll",
                "lib/net40/test.dll",
                "lib/net45/test.dll",
                "lib/netstandard1.0/test.dll",
                "lib/netstandard1.3/test.dll",
                "lib/netstandard2.0/test.dll",
            };

            // Act
            var rule = new DependeciesGroupsForEachTFMRule(AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch,
                                                        AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch);
            byte[] byteArray = Encoding.ASCII.GetBytes(CreateNuspecContent(frameworks));
            MemoryStream stream = new MemoryStream(byteArray);
            var issues = rule.Validate(files, stream).ToList();

            // Assert
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5128));
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5130));
        }

        [Fact]
        public void Validate_PackageWithDependenciesInRefHasExactMatch_ShouldNotWarn()
        {
            var frameworks = new string[]
            {
                ".NETFramework2.0",
                ".NETFramework3.5",
                ".NETFramework4.0",
                ".NETFramework4.5",
                ".NETStandard1.0",
                ".NETStandard1.3",
                ".NETStandard2.0"
            };

            //Arrange
            var files = new string[]
            {
                "ref/net20/test.dll",
                "ref/net35/test.dll",
                "ref/net40/test.dll",
                "ref/net45/test.dll",
                "ref/netstandard1.0/test.dll",
                "ref/netstandard1.3/test.dll",
                "ref/netstandard2.0/test.dll",
            };

            // Act
            var rule = new DependeciesGroupsForEachTFMRule(AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch,
                                                        AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch);
            byte[] byteArray = Encoding.ASCII.GetBytes(CreateNuspecContent(frameworks));
            MemoryStream stream = new MemoryStream(byteArray);
            var issues = rule.Validate(files, stream).ToList();

            // Assert
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5128));
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5130));
        }

        [Fact]
        public void Validate_PackageWithoutDependenciesForEachTFMInLib_ShouldWarnOnce()
        {
            //Arrange
            var frameworks = new string[]
            {
                ".NETFramework2.0",
                ".NETFramework3.5",
                ".NETFramework4.0",
                ".NETFramework4.5",
                ".NETStandard1.0",
                ".NETStandard1.3",
                ".NETStandard2.0"
            };

            var files = new string[]
            {
                "lib/net20/test.dll",
                "lib/net35/test.dll",
                "lib/net40/test.dll"
            };

            // Act
            var rule = new DependeciesGroupsForEachTFMRule(AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch,
                                                        AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch);
            byte[] byteArray = Encoding.ASCII.GetBytes(CreateNuspecContent(frameworks));
            MemoryStream stream = new MemoryStream(byteArray);
            var issues = rule.Validate(files, stream).ToList();

            // Assert
            Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5128));
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5130));
        }

        [Fact]
        public void Validate_PackageWithDependenciesHasCompatMatchNotExactMatch_ShouldWarnTwice()
        {
            var frameworks = new string[]
            {
                ".NETFramework4.5"
            };

            var files = new string[]
            {
                "lib/net472/test.dll"
            };

            // Act
            var rule = new DependeciesGroupsForEachTFMRule(AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch,
                                                        AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch);
            byte[] byteArray = Encoding.ASCII.GetBytes(CreateNuspecContent(frameworks));
            MemoryStream stream = new MemoryStream(byteArray);
            var issues = rule.Validate(files, stream).ToList();

            // Assert
            Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5128 && !p.Message.Contains(".NETFramework4.7.2")));
            Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5130));
        }

        [Fact]
        public void Validate_PackageHasNoDependencyGroupWithFilesInTheLib_ShouldWarnOnce()
        {
            var frameworks = Array.Empty<string>();
            var files = new string[]
            {
                "lib/net20/test.dll",
                "lib/net35/test.dll",
                "lib/net472/test.dll"
            };

            // Act
            var rule = new DependeciesGroupsForEachTFMRule(AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch,
                                                        AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch);
            byte[] byteArray = Encoding.ASCII.GetBytes(CreateNuspecContent(frameworks));
            MemoryStream stream = new MemoryStream(byteArray);
            var issues = rule.Validate(files, stream).ToList();

            // Assert
            Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5128));
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5130));
        }

        [Fact]
        public void Validate_PackageHasCompatMatchFromNuspecToPackage_ShouldWarnOnce()
        {
            var frameworks = new string[]
            {
                ".NETFramework4.7.2"
            };
            var files = new string[]
            {
                "lib/net45/test.dll"
            };
            // Act
            var rule = new DependeciesGroupsForEachTFMRule(AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch,
                                                        AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch);
            byte[] byteArray = Encoding.ASCII.GetBytes(CreateNuspecContent(frameworks));
            MemoryStream stream = new MemoryStream(byteArray);
            var issues = rule.Validate(files, stream).ToList();

            // Assert
            Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5128));
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5130));
        }

        [Fact]
        public void Validate_PackageHasNoDepencyNodeAndHasFilesInLib_ShouldNotWarn()
        {
            var frameworks = Array.Empty<string>();
            var files = new string[]
            {
                "lib/test.dll"
            };
            // Act
            var rule = new DependeciesGroupsForEachTFMRule(AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch,
                                                        AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch);
            byte[] byteArray = Encoding.ASCII.GetBytes(CreateNuspecContent(frameworks));
            MemoryStream stream = new MemoryStream(byteArray);
            var issues = rule.Validate(files, stream).ToList();

            // Assert
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5128));
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5130));
        }

        public static string CreateNuspecContent(string[] frameworks)
        {
            string dependencies = "<dependencies>";
            foreach(var framework in frameworks)
            {
                dependencies = dependencies + "<group targetFramework= \"" + framework + "\" />";
            }
            dependencies = dependencies + "</dependencies>";
            return NuspecContent.Replace("</language>", "</language>" + dependencies);
        }
    }
}


    
