// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.CommandLine.XPlat.Utility;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.XPlat.FuncTest.Utility;

public class DGSpecFactoryTests : IClassFixture<XPlatMsbuildTestFixture>
{
    [Fact]
    public void SingleTargetingProject()
    {
        // Arrange
        using var context = new SimpleTestPathContext();
        var projectPath = Path.Combine(context.SolutionRoot, "my.csproj");

        var projectContents = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>netstandard2.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """;
        File.WriteAllText(projectPath, projectContents);

        // Act
        var dgSpec = DGSpecFactory.Create(projectPath);

        // Assert
        dgSpec.Should().NotBeNull();

        dgSpec.Projects.Should().HaveCount(1);
        dgSpec.Projects[0].TargetFrameworks.Should().HaveCount(1);
        dgSpec.Projects[0].TargetFrameworks[0].TargetAlias.Should().Be("netstandard2.0");

        dgSpec.Restore.Should().BeEquivalentTo([projectPath]);
    }

    [Fact]
    public void MultiTargetingProject()
    {
        // Arrange
        using var context = new SimpleTestPathContext();
        var projectPath = Path.Combine(context.SolutionRoot, "my.csproj");

        var projectContents = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0;net481</TargetFrameworks>
              </PropertyGroup>
            </Project>
            """;
        File.WriteAllText(projectPath, projectContents);

        // Act
        var dgSpec = DGSpecFactory.Create(projectPath);

        // Assert
        dgSpec.Should().NotBeNull();

        dgSpec.Projects.Should().HaveCount(1);
        dgSpec.Projects[0].TargetFrameworks.Should().HaveCount(2);
        dgSpec.Projects[0].TargetFrameworks.Select(tf => tf.TargetAlias).Should().BeEquivalentTo(["net481", "net10.0"]);

        dgSpec.Restore.Should().BeEquivalentTo([projectPath]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SolutionFile(bool useSlnx)
    {
        // Arrange
        using var context = new SimpleTestPathContext();

        var project1 = SimpleTestProjectContext.CreateNETCore(
            "project1",
            context.SolutionRoot,
            NuGet.Frameworks.NuGetFramework.Parse("netstandard2.0"));

        var project2 = SimpleTestProjectContext.CreateNETCore(
            "project2",
            context.SolutionRoot,
            NuGet.Frameworks.NuGetFramework.Parse("net481"),
            NuGet.Frameworks.NuGetFramework.Parse("net8.0"));

        var solution = new SimpleTestSolutionContext(context.SolutionRoot, useSlnx, project1, project2);
        solution.Create();

        var solutionPath = solution.SolutionPath;
        var project1Path = project1.ProjectPath;
        var project2Path = project2.ProjectPath;

        // Act
        var dgSpec = DGSpecFactory.Create(solutionPath);

        // Assert
        dgSpec.Should().NotBeNull();

        dgSpec.Projects.Should().HaveCount(2);

        var project1Result = dgSpec.Projects.Single(p => p.Name == "project1");
        project1Result.TargetFrameworks.Should().HaveCount(1);
        project1Result.TargetFrameworks[0].TargetAlias.Should().Be("netstandard2.0");

        var project2Result = dgSpec.Projects.Single(p => p.Name == "project2");
        project2Result.TargetFrameworks.Should().HaveCount(2);
        project2Result.TargetFrameworks.Select(tf => tf.TargetAlias).Should().BeEquivalentTo(["net481", "net8.0"]);

        dgSpec.Restore.Should().BeEquivalentTo([project1Path, project2Path]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SolutionFilterFile(bool useSlnx)
    {
        // Arrange
        using var context = new SimpleTestPathContext();

        var project1 = SimpleTestProjectContext.CreateNETCore(
            "project1",
            context.SolutionRoot,
            NuGet.Frameworks.NuGetFramework.Parse("netstandard2.0"));

        var project2 = SimpleTestProjectContext.CreateNETCore(
            "project2",
            context.SolutionRoot,
            NuGet.Frameworks.NuGetFramework.Parse("net481"),
            NuGet.Frameworks.NuGetFramework.Parse("net8.0"));

        var solution = new SimpleTestSolutionContext(context.SolutionRoot, useSlnx, project1, project2);
        solution.Create();

        var slnfContent = $$"""
            {
              "solution": {
                "path": "solution.{{(useSlnx ? "slnx" : "sln")}}",
                "projects": [
                  "project1\\project1.csproj"
                ]
              }
            }
            """;
        var slnfPath = Path.Combine(context.SolutionRoot, "filter.slnf");
        File.WriteAllText(slnfPath, slnfContent);

        var project1Path = project1.ProjectPath;

        // Act
        var dgSpec = DGSpecFactory.Create(slnfPath);

        // Assert
        dgSpec.Should().NotBeNull();

        dgSpec.Projects.Should().HaveCount(1);

        var project1Result = dgSpec.Projects.Single(p => p.Name == "project1");
        project1Result.TargetFrameworks.Should().HaveCount(1);
        project1Result.TargetFrameworks[0].TargetAlias.Should().Be("netstandard2.0");

        dgSpec.Restore.Should().BeEquivalentTo([project1Path]);
    }

    // DGSpec.Projects should have the full transitive project graph
    // DGSpec.Restore should only have the root
    [Fact]
    public void ProjectWithProjectReference()
    {
        // Arrange
        using var context = new SimpleTestPathContext();
        var project1 = SimpleTestProjectContext.CreateNETCore(
            "project1",
            context.SolutionRoot,
            NuGet.Frameworks.NuGetFramework.Parse("netstandard2.0"));
        var project2 = SimpleTestProjectContext.CreateNETCore(
            "project2",
            context.SolutionRoot,
            NuGet.Frameworks.NuGetFramework.Parse("netstandard2.0"));
        project2.Frameworks[0].ProjectReferences.Add(project1);

        var solution = new SimpleTestSolutionContext(context.SolutionRoot, project1, project2);
        solution.Create();

        // Act
        var dgSpec = DGSpecFactory.Create(project2.ProjectPath);

        // Assert
        dgSpec.Should().NotBeNull();

        dgSpec.Projects.Should().HaveCount(2);

        var project1Result = dgSpec.Projects.Single(p => p.Name == "project1");
        project1Result.TargetFrameworks.Should().HaveCount(1);

        var project2Result = dgSpec.Projects.Single(p => p.Name == "project2");
        project2Result.TargetFrameworks.Should().HaveCount(1);

        dgSpec.Restore.Should().BeEquivalentTo([project2.ProjectPath]);
    }
}
