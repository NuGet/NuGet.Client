// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using NuGet.CommandLine.XPlat;

namespace Dotnet.Integration.Test;

/// <summary>
/// Test implementation of <see cref="IVirtualProjectBuilder"/>.
/// </summary>
internal sealed class TestVirtualProjectBuilder : IVirtualProjectBuilder, IDisposable
{
    private readonly string _projectContent;

    /// <summary>
    /// The <see cref="ProjectRootElement"/> created by the last call to <see cref="CreateProjectRootElement"/>.
    /// In the real SDK flow, the builder retains this reference so it can read back modifications
    /// after NuGet returns (since <see cref="SaveableProject.Save()"/> is a no-op for virtual projects).
    /// </summary>
    public ProjectRootElement CreatedElement { get; private set; } = null!;

    public TestVirtualProjectBuilder(string projectContent)
    {
        _projectContent = projectContent;
        IVirtualProjectBuilder.SetInstanceForTesting(this);
    }

    public bool IsValidEntryPointPath(string entryPointFilePath)
    {
        return entryPointFilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
    }

    public string GetVirtualProjectPath(string entryPointFilePath)
    {
        return Path.ChangeExtension(entryPointFilePath, ".csproj");
    }

    public ProjectRootElement CreateProjectRootElement(string entryPointFilePath, ProjectCollection projectCollection)
    {
        using var stringReader = new StringReader(_projectContent);
        using var xmlReader = XmlReader.Create(stringReader);
        var element = ProjectRootElement.Create(xmlReader, projectCollection, preserveFormatting: true);
        element.FullPath = GetVirtualProjectPath(entryPointFilePath);
        CreatedElement = element;
        return element;
    }

    public void Dispose()
    {
        IVirtualProjectBuilder.SetInstanceForTesting(null);
    }
}
