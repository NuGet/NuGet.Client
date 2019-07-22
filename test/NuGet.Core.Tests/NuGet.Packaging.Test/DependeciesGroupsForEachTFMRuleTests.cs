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
            var frameworks = new string[]
            {
                ".NETFramework2.0",
                ".NETFramework3.5",
                ".NETFramework4.0",
                ".NETFramework4.5",
                ".NETStandard1.0",
                ".NETStandard1.3",
                ".NETStandard2.0"
            }.Select(f => NuGetFramework.Parse(f));
            //Arrange
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
            var rule = new DependeciesGroupsForEachTFMRule(AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch,
                                                        AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch);
            var issues = rule.Validate(fileStrings, frameworks).ToList();

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
            }.Select(f => NuGetFramework.Parse(f));

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
            var rule = new DependeciesGroupsForEachTFMRule(AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch,
                                                        AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch);
            var issues = rule.Validate(fileStrings, frameworks).ToList();

            // Assert
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5128));
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5130));
        }

        [Fact]
        public void Validate_PackageWithoutDependenciesForEachTFMInLib_ShouldThrowOneWarningCode()
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
            }.Select(f => NuGetFramework.Parse(f));

            var fileStrings = new string[]
            {
                "lib/net20/test.dll",
                "lib/net35/test.dll",
                "lib/net40/test.dll"
            };

            // Act
            var rule = new DependeciesGroupsForEachTFMRule(AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch,
                                                        AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch);
            var issues = rule.Validate(fileStrings, frameworks).ToList();

            // Assert
            Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5128));
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5130));
        }

        [Fact]
        public void Validate_PackageWithDependenciesHasCompatMatchNotExactMatch_ShouldThrowTwoWarningCodes()
        {
            var frameworks = new string[]
            {
                ".NETFramework4.5"
            }.Select(f => NuGetFramework.Parse(f));

            var fileStrings = new string[]
            {
                "lib/net472/test.dll"
            };
            
            // Act
            var rule = new DependeciesGroupsForEachTFMRule(AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch,
                                                        AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch);
            var issues = rule.Validate(fileStrings, frameworks).ToList();

            // Assert
            Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5128 && !p.Message.Contains(".NETFramework4.7.2")));
            Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5130));
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
            var rule = new DependeciesGroupsForEachTFMRule(AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch,
                                                        AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch);
            var issues = rule.Validate(fileStrings, frameworks).ToList();

            // Assert
            Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5128));
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
            var rule = new DependeciesGroupsForEachTFMRule(AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch,
                                                        AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch);
            var issues = rule.Validate(fileStrings, frameworks).ToList();

            // Assert
            Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5128));
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
            var rule = new DependeciesGroupsForEachTFMRule(AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch,
                                                        AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch);
            var issues = rule.Validate(fileStrings, frameworks).ToList();

            // Assert
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5128));
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5130));
        }

        [Fact]
        public void Validate_PackageWithoutDependenciesForEachTFMInLib_ShouldContainCorrectFrameworks()
        {
            //Arrange
            var frameworks = new string[]
            {
                ".NETFramework3.5",
                ".NETFramework4.0",
                ".NETFramework4.5",
            }.Select(f => NuGetFramework.Parse(f));

            var fileStrings = new string[]
            {
                "lib/net20/test.dll",
                "lib/net35/test.dll",
                "lib/net40/test.dll"
            };
            _collection.Load(fileStrings);
            var files = ContentExtractor.GetGroupFrameworks(ContentExtractor.GetContentForPattern(_collection, _managedCodeConventions.Patterns.CompileLibAssemblies));

            // Act
            var rule = new DependeciesGroupsForEachTFMRule(AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch,
                                                        AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch);
            (var testStringExact, var testStringCompat) = rule.Validate(files, frameworks, _managedCodeConventions);

            // Assert
            Assert.True(testStringExact.Contains("Add lib or ref assemblies for the net45 target framework"));
            Assert.True(testStringExact.Contains(" Add a dependency group for .NETFramework2.0 to the nuspec"));
            Assert.True(testStringCompat == string.Empty);
        }
    }
}