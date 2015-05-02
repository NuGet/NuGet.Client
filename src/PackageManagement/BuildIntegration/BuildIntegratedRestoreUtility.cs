using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Framework.Logging;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Helper class for calling DNU restore
    /// </summary>
    public static class BuildIntegratedRestoreUtility
    {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public static async Task RestoreForBuild(string jsonConfigPath,
            string projectName,
            INuGetProjectContext projectContext,
            IEnumerable<string> additionalSources,
            CancellationToken token)
        {
            await Restore(jsonConfigPath, projectName, projectContext, additionalSources, token);
        }

        public static async Task<RestoreResult> Restore(string jsonConfigPath,
            string projectName,
            INuGetProjectContext projectContext,
            IEnumerable<string> additionalSources, 
            CancellationToken token)
        {
            // Limit to only 1 restore at a time
            await _semaphore.WaitAsync(token);

            try
            {
                token.ThrowIfCancellationRequested();

                return await RestoreCore(jsonConfigPath, projectName, projectContext, additionalSources, token);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static async Task<RestoreResult> RestoreCore(string jsonConfigPath, string projectName, INuGetProjectContext projectContext, IEnumerable<string> sources, CancellationToken token)
        {
            FileInfo file = new FileInfo(jsonConfigPath);

            PackageSpec spec = JsonPackageSpecReader.GetPackageSpec(file.OpenRead(), projectName, jsonConfigPath);

            RestoreRequest request = new RestoreRequest(spec, sources.Select(source => new PackageSource(source)), file.Directory.FullName);

            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new ProjectContextLoggerProvider(projectContext));

            RestoreCommand command = new RestoreCommand(loggerFactory);

            return await command.ExecuteAsync(request);
        }

        /// <summary>
        /// nupkg path from the global cache folder
        /// </summary>
        public static string GetNupkgPathFromGlobalSource(PackageIdentity identity)
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string nupkgName = identity.Id + "." + identity.Version.ToNormalizedString() + ".nupkg";

            return Path.Combine(GlobalPackagesFolder, identity.Id, identity.Version.ToNormalizedString(), nupkgName);
        }

        /// <summary>
        /// Global package folder path
        /// </summary>
        public static string GlobalPackagesFolder
        {
            get
            {
                string path = Environment.GetEnvironmentVariable("NUGET_GLOBAL_PACKAGE_CACHE");

                if (String.IsNullOrEmpty(path))
                {
                    string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                    path = Path.Combine(userProfile, ".nuget\\packages\\");
                }

                return path;
            }
        }
    }
}
