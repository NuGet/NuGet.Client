// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet.Integration.Test;

[Collection(DotnetIntegrationCollection.Name)]
public sealed class DotnetRemovePackageTests(DotnetIntegrationTestFixture fixture, ITestOutputHelper testOutputHelper)
{
    private readonly DotnetIntegrationTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _testOutputHelper = testOutputHelper;

    [Fact]
    public void RemovePkg_FileBasedApp()
    {
        using var pathContext = _fixture.CreateSimpleTestPathContext();

        // Create the file-based app.
        var fbaDir = Path.Join(pathContext.SolutionRoot, "fba");
        Directory.CreateDirectory(fbaDir);

        var appFile = Path.Join(fbaDir, "app.cs");
        File.WriteAllText(appFile, """
            #:package packageX@1.0.0
            Console.WriteLine();
            """);

        // Remove the package.
        _fixture.RunDotnetExpectSuccess(fbaDir, "package remove packageX --file app.cs", testOutputHelper: _testOutputHelper);

        // Verify the full content of the modified .cs file.
        var modifiedContent = File.ReadAllText(appFile);
        _testOutputHelper.WriteLine("after:\n" + modifiedContent);
        Assert.Equal(
            """
            Console.WriteLine();
            """,
            modifiedContent);
    }

    [Fact]
    public void RemovePkg_FileBasedApp_WithRef()
    {
        using var pathContext = _fixture.CreateSimpleTestPathContext();

        // Create a referenced file-based app.
        var libDir = Path.Join(pathContext.SolutionRoot, "lib");
        Directory.CreateDirectory(libDir);

        var libFile = Path.Join(libDir, "lib.cs");
        var libContent = """
            #:property PublishAot=false
            #:package packageY@1.0.0
            public class Lib { }
            """;
        File.WriteAllText(libFile, libContent);

        // Create the root file-based app referencing the lib.
        var fbaDir = Path.Join(pathContext.SolutionRoot, "fba");
        Directory.CreateDirectory(fbaDir);

        var refPath = Path.GetRelativePath(fbaDir, libFile);
        var appFile = Path.Join(fbaDir, "app.cs");
        var appContentPre = $"""
            #:property PublishAot=false
            #:property ExperimentalFileBasedProgramEnableRefDirective=true
            #:ref {refPath}
            """;
        var appContentPost = """
            Console.WriteLine();
            """;
        File.WriteAllText(appFile, $"""
            {appContentPre}
            #:package packageX@1.0.0
            {appContentPost}
            """);

        // Remove the package from the root app.
        _fixture.RunDotnetExpectSuccess(fbaDir, "package remove packageX --file app.cs", testOutputHelper: _testOutputHelper);

        // Verify the package was removed from the root app.
        var modifiedAppContent = File.ReadAllText(appFile);
        _testOutputHelper.WriteLine("app after:\n" + modifiedAppContent);
        Assert.Equal(
            $"""
            {appContentPre}
            {appContentPost}
            """,
            modifiedAppContent);

        // Verify the lib was not modified.
        var modifiedLibContent = File.ReadAllText(libFile);
        _testOutputHelper.WriteLine("lib after:\n" + modifiedLibContent);
        Assert.Equal(libContent, modifiedLibContent);
    }
}
