// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging.Rules;
using Xunit;
using NuGet.RuntimeModel;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Packaging.Core;

namespace NuGet.Packaging.Test
{
    public class DependeciesGroupsForEachTFMRuleTests
    {
        ManagedCodeConventions _managedCodeConventions = new ManagedCodeConventions(new RuntimeGraph());
        ContentItemCollection _collection = new ContentItemCollection();

        [Fact]
        public void Validate_PackageWithDependenciesForEachTFMInLib_ShouldNotWarn()
        {
            //Arrange
            var frameworks = new NuGetFramework[]
            {
                FrameworkConstants.CommonFrameworks.Net2,
                FrameworkConstants.CommonFrameworks.Net35,
                FrameworkConstants.CommonFrameworks.Net4,
                FrameworkConstants.CommonFrameworks.Net45,
                FrameworkConstants.CommonFrameworks.NetStandard10,
                FrameworkConstants.CommonFrameworks.NetStandard13,
                FrameworkConstants.CommonFrameworks.NetStandard20,
            };

            var fileStrings = new string[]
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
            var rule = new DependeciesGroupsForEachTFMRule();
            var (compat, file, nuspec) = rule.CatagoriseTFMs(fileStrings, frameworks);
            var issues = rule.GenerateWarnings(compat, file, nuspec).ToList();

            // Assert
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5128));
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5130));
        }

        [Fact]
        public void Validate_PackageWithDependenciesInRefHasExactMatch_ShouldNotWarn()
        {
            var frameworks = new NuGetFramework[]
            {
                FrameworkConstants.CommonFrameworks.Net2,
                FrameworkConstants.CommonFrameworks.Net35,
                FrameworkConstants.CommonFrameworks.Net4,
                FrameworkConstants.CommonFrameworks.Net45,
                FrameworkConstants.CommonFrameworks.NetStandard10,
                FrameworkConstants.CommonFrameworks.NetStandard13,
                FrameworkConstants.CommonFrameworks.NetStandard20,
            };

            //Arrange
            var fileStrings = new string[]
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
            var rule = new DependeciesGroupsForEachTFMRule();
            var (compat, file, nuspec) = rule.CatagoriseTFMs(fileStrings, frameworks);
            var issues = rule.GenerateWarnings(compat, file, nuspec).ToList();

            // Assert
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5128));
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5130));
        }

        [Fact]
        public void Validate_PackageWithoutDependenciesForEachTFMInLib_ShouldThrowOneWarningCode()
        {
            //Arrange
            var frameworks = new NuGetFramework[]
            {
                FrameworkConstants.CommonFrameworks.Net2,
                FrameworkConstants.CommonFrameworks.Net35,
                FrameworkConstants.CommonFrameworks.Net4,
                FrameworkConstants.CommonFrameworks.Net45,
                FrameworkConstants.CommonFrameworks.NetStandard10,
                FrameworkConstants.CommonFrameworks.NetStandard13,
                FrameworkConstants.CommonFrameworks.NetStandard20,
            };

            var fileStrings = new string[]
            {
                "lib/net20/test.dll",
                "lib/net35/test.dll",
                "lib/net40/test.dll"
            };

            // Act
            var rule = new DependeciesGroupsForEachTFMRule();
            var (compat, file, nuspec) = rule.CatagoriseTFMs(fileStrings, frameworks);
            var issues = rule.GenerateWarnings(compat, file, nuspec).ToList();

            // Assert
            var testStringExact = AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch + "\n" +
                "- Add lib or ref assemblies for the net45 target framework\r\n" +
                "- Add lib or ref assemblies for the netstandard1.0 target framework\r\n" +
                "- Add lib or ref assemblies for the netstandard1.3 target framework\r\n" +
                "- Add lib or ref assemblies for the netstandard2.0 target framework\r\n";
            Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5128 && p.Message == testStringExact));
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5130));
        }

        [Fact]
        public void Validate_PackageWithDependenciesHasCompatMatchNotExactMatch_ShouldThrowTwoWarningCodes()
        {
            var frameworks = new NuGetFramework[]
            {
                FrameworkConstants.CommonFrameworks.Net45,
            };

            var fileStrings = new string[]
            {
                "lib/net472/test.dll"
            };

            // Act
            var rule = new DependeciesGroupsForEachTFMRule();
            var (compat, file, nuspec) = rule.CatagoriseTFMs(fileStrings, frameworks);
            var issues = rule.GenerateWarnings(compat, file, nuspec).ToList();

            // Assert
            var testStringExact = AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch + "\n" +
                "- Add lib or ref assemblies for the net45 target framework\r\n";
            var testStringCompat = AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch + "\n" +
                "- Add a dependency group for .NETFramework4.7.2 to the nuspec\r\n";
            Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5128 && p.Message == testStringExact));
            Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5130 && p.Message == testStringCompat));
        }

        [Fact]
        public void Validate_PackageHasNoDependencyGroupWithFilesInTheLib_ShouldThrowOneWarningCode()
        {
            var frameworks = Array.Empty<NuGetFramework>();
            var fileStrings = new string[]
            {
                "lib/net20/test.dll",
                "lib/net35/test.dll",
                "lib/net472/test.dll"
            };

            // Act
            var rule = new DependeciesGroupsForEachTFMRule();
            var (compat, file, nuspec) = rule.CatagoriseTFMs(fileStrings, frameworks);
            var issues = rule.GenerateWarnings(compat, file, nuspec).ToList();

            // Assert
            var testStringExact = AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch + "\n" +
                "- Add a dependency group for .NETFramework2.0 to the nuspec\r\n" +
                "- Add a dependency group for .NETFramework3.5 to the nuspec\r\n" +
                "- Add a dependency group for .NETFramework4.7.2 to the nuspec\r\n";
            Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5128 && p.Message == testStringExact));
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5130));
        }

        [Fact]
        public void Validate_PackageHasCompatMatchFromNuspecToPackage_ShouldThrowOneWarningCode()
        {
            var frameworks = new string[]
            {
                ".NETFramework4.7.2"
            }.Select(f => NuGetFramework.Parse(f));

            var fileStrings = new string[]
            {
                "lib/net45/test.dll"
            };

            // Act
            var rule = new DependeciesGroupsForEachTFMRule();
            var (compat, file, nuspec) = rule.CatagoriseTFMs(fileStrings, frameworks);
            var issues = rule.GenerateWarnings(compat, file, nuspec).ToList();

            // Assert
            var testStringExact = AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch + "\n" +
                "- Add a dependency group for .NETFramework4.5 to the nuspec\r\n" +
                "- Add lib or ref assemblies for the net472 target framework\r\n";
            Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5128 && p.Message == testStringExact));
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5130));
        }

        [Fact]
        public void Validate_PackageHasNoDepencyNodeAndHasFilesInLib_ShouldNotWarn()
        {
            var frameworks = Array.Empty<NuGetFramework>();
            var fileStrings = new string[]
            {
                "lib/test.dll"
            };

            // Act
            var rule = new DependeciesGroupsForEachTFMRule();
            var (compat, file, nuspec) = rule.CatagoriseTFMs(fileStrings, frameworks);
            var issues = rule.GenerateWarnings(compat, file, nuspec).ToList();

            // Assert
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5128));
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5130));
        }

        [Fact]
        public void Validate_PackageWithoutDependenciesForEachTFMInLib_ShouldContainCorrectFrameworks()
        {
            //Arrange
            var frameworks = new NuGetFramework[]
            {
                FrameworkConstants.CommonFrameworks.Net35,
                FrameworkConstants.CommonFrameworks.Net4,
                FrameworkConstants.CommonFrameworks.Net45,
            };

            var files = new string[]
            {
                "lib/net20/test.dll",
                "lib/net35/test.dll",
                "lib/net40/test.dll"
            };
            

            // Act
            var rule = new DependeciesGroupsForEachTFMRule();
            var (compat, file, nuspec) = rule.CatagoriseTFMs(files, frameworks);
            var (testStringExact, testStringCompat) = rule.GenerateWarningString(file, nuspec, compat);

            // Assert
            Assert.True(testStringExact.Contains("Add lib or ref assemblies for the net45 target framework"));
            Assert.True(testStringExact.Contains(" Add a dependency group for .NETFramework2.0 to the nuspec"));
            Assert.True(testStringCompat == string.Empty);
        }
    }
}
