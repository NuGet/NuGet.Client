// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    public interface IScriptExecutor
    {
        Task<bool> ExecuteAsync(PackageIdentity packageIdentity, string packageInstallPath, string scriptRelativePath, EnvDTEProject envDTEProject, NuGetProject nuGetProject, INuGetProjectContext nuGetProjectContext, bool throwOnFailure);
    }
}
