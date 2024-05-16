// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.CommandLine.XPlat.WhyCommandUtility;
using NuGet.ProjectModel;
using Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class XPlatWhyUnitTests
    {
        [Fact]
        public void WhyCommand_DependencyGraphFinder_MultipleDependencyPathsForTargetPackage_AllPathsFound()
        {
            // Arrange
            var lockFileFormat = new LockFileFormat();
            var lockFileContent = ProtocolUtility.GetResource("NuGet.CommandLine.Xplat.Tests.compiler.resources.DNW.Test.SampleProject1.project.assets.json", GetType());
            var assetsFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            string targetPackage = "System.Text.Json";
            var frameworks = new List<string>();

            // Act
            var dependencyGraphs = DependencyGraphFinder.GetAllDependencyGraphs(assetsFile, targetPackage, frameworks);

            // Assert

            // direct dependency on target package is found
            Assert.Contains(dependencyGraphs["net472"], dep => (dep.Id == "System.Text.Json") && (dep.Version == "8.0.0"));

            // transitive dependency from a top-level package is found, with the correct resolved version
            Assert.Contains(dependencyGraphs["net472"].First(dep => dep.Id == "Azure.Core").Children, dep => (dep.Id == "System.Text.Json") && (dep.Version == "8.0.0"));

            // transitive dependency from a top-level project reference is found, with the correct resolved version
            Assert.Contains(dependencyGraphs["net472"].First(dep => dep.Id == "DotnetNuGetWhyPackage").Children, dep => (dep.Id == "System.Text.Json") && (dep.Version == "8.0.0"));
        }

        [Fact]
        public void WhyCommand_DependencyGraphFinder_DependencyOnTargetProject_AllPathsFound()
        {
            // Arrange
            var lockFileFormat = new LockFileFormat();
            var lockFileContent = ProtocolUtility.GetResource("NuGet.CommandLine.Xplat.Tests.compiler.resources.DNW.Test.SampleProject1.project.assets.json", GetType());
            var assetsFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            string targetPackage = "DotnetNuGetWhyPackage"; // project reference
            var frameworks = new List<string>();

            // Act
            var dependencyGraphs = DependencyGraphFinder.GetAllDependencyGraphs(assetsFile, targetPackage, frameworks);

            // Assert

            // direct dependency on target project reference is found
            Assert.Contains(dependencyGraphs["net472"], dep => dep.Id == "DotnetNuGetWhyPackage");

            // transitive dependency on target project reference is found
            Assert.Contains(dependencyGraphs["net472"].First(dep => dep.Id == "CustomProjectName").Children, dep => dep.Id == "DotnetNuGetWhyPackage");
        }

        [Fact]
        public void WhyCommand_DependencyGraphFinder_NoDependencyOnTargetPackage_ReturnsNullGraph()
        {
            // Arrange
            var lockFileFormat = new LockFileFormat();
            var lockFileContent = ProtocolUtility.GetResource("NuGet.CommandLine.Xplat.Tests.compiler.resources.DNW.Test.SampleProject1.project.assets.json", GetType());
            var assetsFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            string targetPackage = "NotARealPackage";
            var frameworks = new List<string>();

            // Act
            var dependencyGraphs = DependencyGraphFinder.GetAllDependencyGraphs(assetsFile, targetPackage, frameworks);

            // Assert

            // no paths found for any framework
            Assert.Null(dependencyGraphs);
        }

        [Fact]
        public void WhyCommand_DependencyGraphFinder_DependencyOnTargetPackageForOnlyOneFramework_ReturnsCorrectGraphs()
        {
            // Arrange
            var lockFileFormat = new LockFileFormat();
            var lockFileContent = ProtocolUtility.GetResource("NuGet.CommandLine.Xplat.Tests.compiler.resources.DNW.Test.SampleProject1.project.assets.json", GetType());
            var assetsFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            string targetPackage = "Azure.Core";
            var frameworks = new List<string>();

            // Act
            var dependencyGraphs = DependencyGraphFinder.GetAllDependencyGraphs(assetsFile, targetPackage, frameworks);

            // Assert

            // paths found for one framework
            Assert.Contains(dependencyGraphs["net472"], dep => dep.Id == "Azure.Core");

            // no paths found for the other framework
            Assert.Null(dependencyGraphs["net6.0"]);
        }

        [Fact]
        public void WhyCommand_DependencyGraphFinder_DependencyOnTargetPackage_FrameworkOptionSpecified_PathIsFound()
        {
            // Arrange
            var lockFileFormat = new LockFileFormat();
            var lockFileContent = ProtocolUtility.GetResource("NuGet.CommandLine.Xplat.Tests.compiler.resources.DNW.Test.SampleProject1.project.assets.json", GetType());
            var assetsFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            string targetPackage = "Azure.Core";
            List<string> frameworks = ["net472"];

            // Act
            var dependencyGraphs = DependencyGraphFinder.GetAllDependencyGraphs(assetsFile, targetPackage, frameworks);

            // Assert

            // Path found
            Assert.Contains(dependencyGraphs["net472"], dep => dep.Id == "Azure.Core");
        }

        [Fact]
        public void WhyCommand_DependencyGraphFinder_DependencyOnTargetPackageForOnlyOneFramework_DifferentFrameworkSpecified_ReturnsNullGraph()
        {
            // Arrange
            var lockFileFormat = new LockFileFormat();
            var lockFileContent = ProtocolUtility.GetResource("NuGet.CommandLine.Xplat.Tests.compiler.resources.DNW.Test.SampleProject1.project.assets.json", GetType());
            var assetsFile = lockFileFormat.Parse(lockFileContent, "In Memory");

            string targetPackage = "Azure.Core";
            List<string> frameworks = ["net6.0"];

            // Act
            var dependencyGraphs = DependencyGraphFinder.GetAllDependencyGraphs(assetsFile, targetPackage, frameworks);

            // Assert

            // no paths found
            Assert.Null(dependencyGraphs);
        }
    }
}
