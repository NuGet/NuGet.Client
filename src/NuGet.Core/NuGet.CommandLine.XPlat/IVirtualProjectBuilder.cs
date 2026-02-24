// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
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
    bool IsValidEntryPointPath(string entryPointFilePath);

    ProjectRootElement CreateProjectRootElement(string entryPointFilePath, ProjectCollection projectCollection);

    internal static IVirtualProjectBuilder? TryLoad()
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
}
