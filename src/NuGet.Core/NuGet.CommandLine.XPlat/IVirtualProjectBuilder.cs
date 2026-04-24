// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.IO;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace NuGet.CommandLine.XPlat;

/// <summary>
/// We cannot have a dependency on a package from SDK due to source build,
/// hence we invert the relationship and define the interface here,
/// SDK implements it and we load the implementation dynamically.
/// </summary>
public interface IVirtualProjectBuilder
{
    /// <summary>
    /// Whether the given file path can be a file-based app.
    /// </summary>
    /// <remarks>
    /// Currently, files that exist and have the <c>.cs</c> file extension or <c>#!</c> (shebang) are valid file-based apps.
    /// </remarks>
    bool IsValidEntryPointPath(string entryPointFilePath);

    /// <summary>
    /// Returns the virtual project path (e.g., <c>app.csproj</c>) corresponding to the given entry point file
    /// (e.g., <c>app.cs</c>). The returned path is used by MSBuild for DG specs and property evaluation.
    /// </summary>
    string GetVirtualProjectPath(string entryPointFilePath);

    /// <summary>
    /// Given a virtual project path (e.g., <c>app.csproj</c>), attempts to find the corresponding
    /// entry point file path (e.g., <c>app.cs</c>). Returns <c>null</c> if no valid entry point exists.
    /// </summary>
    /// <remarks>
    /// This is the reverse of <see cref="GetVirtualProjectPath"/>.
    /// </remarks>
    string? TryGetEntryPointPath(string virtualProjectPath)
    {
        // Default implementation for backward compatibility with SDK versions
        // that don't yet implement this method.
        string potentialEntryPoint = Path.ChangeExtension(virtualProjectPath, ".cs");
        return IsValidEntryPointPath(potentialEntryPoint) ? potentialEntryPoint : null;
    }

    ProjectRootElement CreateProjectRootElement(string entryPointFilePath, ProjectCollection projectCollection);

    void SaveProject(string entryPointFilePath, ProjectRootElement projectRootElement);
}
