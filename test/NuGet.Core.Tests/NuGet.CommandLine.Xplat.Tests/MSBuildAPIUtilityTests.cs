// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;
using NuGet.CommandLine.XPlat;
using NuGet.Test.Utility;
using Xunit;
using Project = Microsoft.Build.Evaluation.Project;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class MSBuildAPIUtilityTests
    {
        static MSBuildAPIUtilityTests()
        {
            MSBuildLocator.RegisterDefaults();
        }

        [Fact]
        public void MSBuilldTest()
        {
            var testDirectory = TestDirectory.Create();

            var projectCollection = new ProjectCollection(
                            globalProperties: null,
                            remoteLoggers: null,
                            loggers: null,
                            toolsetDefinitionLocations: ToolsetDefinitionLocations.Default,
                            // Having more than 1 node spins up multiple msbuild.exe instances to run builds in parallel
                            // However, these targets complete so quickly that the added overhead makes it take longer
                            maxNodeCount: 1,
                            onlyLogCriticalEvents: false,
                            loadProjectsReadOnly: false);

            var projectOptions = new ProjectOptions
            {
                LoadSettings = ProjectLoadSettings.DoNotEvaluateElementsWithFalseCondition,
                ProjectCollection = projectCollection
            };

            var propsFile =
@$"<Project>
    <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    </PropertyGroup>
</Project>";
            File.WriteAllText(Path.Combine(testDirectory, "Directory.Packages.props"), propsFile);

            string projectContent =
@$"<Project Sdk=""Microsoft.NET.Sdk"">    
	<PropertyGroup>                    
	<OutputType>Exe</OutputType>
	<TargetFramework>net6.0</TargetFramework>
	<ImplicitUsings>enable</ImplicitUsings>
	<Nullable>enable</Nullable>	
	</PropertyGroup>
</Project>";
            File.WriteAllText(Path.Combine(testDirectory, "projectA.csproj"), projectContent);

            var project = Project.FromFile(Path.Combine(testDirectory, "projectA.csproj"), projectOptions);

            var yes = new MSBuildAPIUtility(logger: new TestLogger()).GetDirectoryBuildPropsRootElement(project);

            Assert.Equal(Path.Combine(testDirectory, "Directory.Packages.props"), yes.FullPath);
        }
    }
}
