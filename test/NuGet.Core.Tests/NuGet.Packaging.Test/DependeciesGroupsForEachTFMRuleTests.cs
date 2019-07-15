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
        string _nuspecContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
"<package xmlns=\"http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd\">" +
"   <metadata>" +
"        <id>test</id>" +
"        <version>1.0.0</version>" +
"        <authors>Unit Test</authors>" +
"        <description>Sample Description</description>" +
"        <language>en-US</language>" +
"    <dependencies>" +
"      <group targetFramework= \".NETFramework2.0\" />" +
"       <group targetFramework = \".NETFramework3.5\" />" +
"       <group targetFramework = \".NETFramework4.0\" />" +
"       <group targetFramework = \".NETFramework4.5\" />" +
"       <group targetFramework = \".NETStandard1.0\">" +
"           <dependency id = \"Microsoft.CSharp\" version=\"4.3.0\" />" +
"        </group>" +
"       <group targetFramework = \".NETStandard1.3\">" +
"            <dependency id = \"Microsoft.CSharp\" version=\"4.3.0\" />" +
"       </group>" +
"       <group targetFramework = \".NETStandard2.0\" />" +
"    </dependencies>" +
"    </metadata>" +
"</package>";

  

        [Fact]
        public void Validate_PackageWithDependenciesForEachTFMInLib_ShouldNotWarn()
        {
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
                "ref/net20/test.dll"
            };

            // Act
            var rule = new DependeciesGroupsForEachTFMRule(AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch,
                                                        AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch);
            byte[] byteArray = Encoding.ASCII.GetBytes(_nuspecContent);
            MemoryStream stream = new MemoryStream(byteArray);
            var issues = rule.Validate(files, stream).ToList();

            // Assert
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5128));
        }

        [Fact]
        public void Validate_PackageWithoutDependenciesForEachTFMInLib_ShouldWarn()
        {
            //Arrange
            var files = new string[]
            {
                "lib/net20/test.dll",
                "lib/net35/test.dll",
                "lib/net40/test.dll"
            };

            // Act
            var rule = new DependeciesGroupsForEachTFMRule(AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch,
                                                        AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch);
            byte[] byteArray = Encoding.ASCII.GetBytes(_nuspecContent);
            MemoryStream stream = new MemoryStream(byteArray);
            var issues = rule.Validate(files, stream).ToList();

            // Assert
            Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5128));
        }
    }

}
