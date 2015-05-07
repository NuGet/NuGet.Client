// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO.Compression;
using System.Threading.Tasks;
using NuGet.ProjectManagement;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    public interface IScriptExecutor
    {
        Task<bool> ExecuteAsync(string packageInstallPath, string scriptRelativePath, ZipArchive packageZipArchive, EnvDTEProject envDTEProject, NuGetProject nuGetProject, INuGetProjectContext nuGetProjectContext, bool throwOnFailure);
    }
}
