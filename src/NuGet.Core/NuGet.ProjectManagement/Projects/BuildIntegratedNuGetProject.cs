// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.ProjectManagement.Projects
{
    /// <summary>
    /// A NuGet integrated MSBuild project.k
    /// These projects contain a project.json or package references in CSProj
    /// </summary>
    public abstract class BuildIntegratedNuGetProject : NuGetProject, INuGetIntegratedProject, IDependencyGraphProject
    {
        protected BuildIntegratedNuGetProject() { }

        /// <summary>
        /// Project name
        /// </summary>
        public abstract string ProjectName { get; }

        public abstract string MSBuildProjectPath { get; }

        /// <summary>
        /// Returns the path to the assets file or the lock file.
        /// </summary>
        public abstract string AssetsFilePath { get; }

        public abstract DateTimeOffset LastModified { get; }
        public abstract Task<IReadOnlyList<PackageSpec>> GetPackageSpecsAsync(DependencyGraphCacheContext context);

        //public abstract Task<IReadOnlyList<IDependencyGraphProject>> GetDirectProjectReferencesAsync(DependencyGraphCacheContext context);
        public abstract Task<bool> IsRestoreRequired(IEnumerable<VersionFolderPathResolver> pathResolvers, ISet<PackageIdentity> packagesChecked, DependencyGraphCacheContext context);

        /// <summary>
        /// Script executor hook
        /// </summary>
        public abstract Task<bool> ExecuteInitScriptAsync(
            PackageIdentity identity,
            string packageInstallPath,
            INuGetProjectContext projectContext,
            bool throwOnFailure);

        //public virtual async Task<DependencyGraphSpec> GetDependencyGraphSpecAsync(DependencyGraphCacheContext context)
        //{
        //    DependencyGraphSpec dgSpec = null;
        //    if (context == null || !context.DependencyGraphCache.TryGetValue(MSBuildProjectPath, out dgSpec))
        //    {
        //        var projectReferences = await GetDirectProjectReferencesAsync(context);
        //        var listOfDgSpecs = projectReferences.Select(async r => await r.GetDependencyGraphSpecAsync(context)).Select(r => r.Result).ToList();

        //        dgSpec = DependencyGraphSpec.Union(listOfDgSpecs);
        //        dgSpec.AddProject(await GetPackageSpecAsync(context));
        //        dgSpec.AddRestore(MSBuildProjectPath);

        //        //Cache this DG File
        //        context?.DependencyGraphCache.Add(MSBuildProjectPath, dgSpec);
        //    }

        //    return dgSpec;
        //}
    }
}