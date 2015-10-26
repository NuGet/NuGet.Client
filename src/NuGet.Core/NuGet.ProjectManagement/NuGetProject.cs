// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.ProjectManagement
{
    public abstract class NuGetProject
    {
        protected NuGetProject()
            : this(new Dictionary<string, object>())
        {
        }

        protected NuGetProject(Dictionary<string, object> metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            InternalMetadata = metadata;
        }

        protected Dictionary<string, object> InternalMetadata { get; }

        public IReadOnlyDictionary<string, object> Metadata
        {
            get { return InternalMetadata; }
        }

        // TODO: Consider adding CancellationToken here
        /// <summary>
        /// This installs a package into the NuGetProject using the <see cref="Stream"/> passed in
        /// <param name="downloadResourceResult"></param>
        /// should be seekable
        /// </summary>
        /// <returns>
        /// Returns false if the package was already present in the NuGetProject. On successful installation,
        /// returns true
        /// </returns>
        public abstract Task<bool> InstallPackageAsync(
            PackageIdentity packageIdentity,
            DownloadResourceResult downloadResourceResult,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token);

        /// <summary>
        /// This uninstalls the package from the NuGetProject, if found
        /// </summary>
        /// <returns>Returns false if the package was not found. On successful uninstallation, returns true</returns>
        public abstract Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token);

        /// <summary>
        /// GetInstalledPackages will be used by Dependency Resolver and more
        /// </summary>
        /// <returns></returns>
        public abstract Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token);

        public virtual Task PreProcessAsync(INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            // Do Nothing by default
            return Task.FromResult(0);
        }

        public virtual Task PostProcessAsync(INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            // Do Nothing by default
            return Task.FromResult(0);
        }

        public T GetMetadata<T>(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            var value = Metadata[key];
            return (T)value;
        }

        public bool TryGetMetadata<T>(string key, out T value)
        {
            value = default(T);

            object oValue;
            if (Metadata.TryGetValue(key, out oValue))
            {
                if (oValue == null)
                {
                    return true;
                }

                if (oValue is T)
                {
                    value = (T)oValue;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// This static helper method returns the unique name on the project if present
        /// Otherwise, returns the name. If name is not present, it will throw
        /// </summary>
        /// <param name="nuGetProject"></param>
        /// <returns></returns>
        public static string GetUniqueNameOrName(NuGetProject nuGetProject)
        {
            if (nuGetProject == null)
            {
                throw new ArgumentNullException("nuGetProject");
            }

            string nuGetProjectName;
            if (!nuGetProject.TryGetMetadata(NuGetProjectMetadataKeys.UniqueName, out nuGetProjectName))
            {
                // Unique name is not set, simply return the name
                nuGetProjectName = nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
            }

            return nuGetProjectName;
        }
    }

    public static class NuGetProjectMetadataKeys
    {
        // The name of the project, e.g. "ConsoleApplication1"
        public const string Name = "Name";

        // The name of the project, relative to the solution. e.g. "src\ConsoleApplication1"
        public const string UniqueName = "UniqueName";

        public const string TargetFramework = "TargetFramework";
        public const string FullPath = "FullPath";

        // used by Project K projects
        public const string SupportedFrameworks = "SupportedFrameworks";
    }
}
