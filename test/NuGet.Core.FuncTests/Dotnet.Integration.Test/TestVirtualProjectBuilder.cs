// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using NuGet.CommandLine.XPlat;
using Xunit;

namespace Dotnet.Integration.Test;

/// <summary>
/// Test implementation of <see cref="IVirtualProjectBuilder"/>.
/// </summary>
internal sealed class TestVirtualProjectBuilder : IVirtualProjectBuilder
{
    private readonly (string Content, string ProjectPath, string FilePath) _virtualProject;

    public string? ModifiedContent { get; private set; }

    public TestVirtualProjectBuilder((string Content, string ProjectPath, string FilePath) virtualProject)
    {
        _virtualProject = virtualProject;
    }

    public bool IsValidEntryPointPath(string entryPointFilePath)
    {
        return entryPointFilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
    }

    public string GetVirtualProjectPath(string entryPointFilePath)
    {
        return _virtualProject.ProjectPath;
    }

    public ProjectRootElement CreateProjectRootElement(string entryPointFilePath, ProjectCollection projectCollection)
    {
        using var stringReader = new StringReader(_virtualProject.Content);
        using var xmlReader = XmlReader.Create(stringReader);
        var element = ProjectRootElement.Create(xmlReader, projectCollection, preserveFormatting: true);
        element.FullPath = GetVirtualProjectPath(entryPointFilePath);
        return element;
    }

    public void SaveProject(string entryPointFilePath, ProjectRootElement projectRootElement)
    {
        Assert.Equal(_virtualProject.FilePath, entryPointFilePath);
        Assert.Null(ModifiedContent); // ideally we should not be saving twice
        ModifiedContent = projectRootElement.RawXml;
    }
}
