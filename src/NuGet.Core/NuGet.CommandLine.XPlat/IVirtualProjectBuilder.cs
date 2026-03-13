// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
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

    ProjectRootElement CreateProjectRootElement(string entryPointFilePath, ProjectCollection projectCollection);

    private static IVirtualProjectBuilder? Instance;

    internal static IVirtualProjectBuilder? GetInstance()
    {
        return Instance ??= LoadFromDotnetDll();
    }

    [SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "The target type in dotnet CLI is marked as non-trimmable.")]
    [SuppressMessage("Trimming", "IL2072:Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.")]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    private static IVirtualProjectBuilder? LoadFromDotnetDll()
    {
        try
        {
            var assemblyPath = Path.Join(AppContext.BaseDirectory, "dotnet.dll");

            if (!File.Exists(assemblyPath))
            {
                return null;
            }

            var type = Assembly.LoadFile(assemblyPath)
                .GetExportedTypes()
                .FirstOrDefault(static t => t.IsAssignableTo(typeof(IVirtualProjectBuilder)));
            return type != null ? (IVirtualProjectBuilder?)Activator.CreateInstance(type) : null;
        }
        catch
        {
            return null;
        }
    }
}
