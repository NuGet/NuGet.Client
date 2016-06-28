// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    public class PackageRestoreManager : IPackageRestoreManager
    {
        private const string NuGetSolutionSettingsFolder = ".nuget";
        private static readonly string NuGetExeFile = Path.Combine(NuGetSolutionSettingsFolder, "NuGet.exe");
        private static readonly string NuGetTargetsFile = Path.Combine(NuGetSolutionSettingsFolder, "NuGet.targets");

        private ISourceRepositoryProvider SourceRepositoryProvider { get; }
        private ISolutionManager SolutionManager { get; }
        private Configuration.ISettings Settings { get; }

        public event EventHandler<PackagesMissingStatusEventArgs> PackagesMissingStatusChanged;
        public event EventHandler<PackageRestoredEventArgs> PackageRestoredEvent;
        public event EventHandler<PackageRestoreFailedEventArgs> PackageRestoreFailedEvent;

        public PackageRestoreManager(
            ISourceRepositoryProvider sourceRepositoryProvider,
            Configuration.ISettings settings,
            ISolutionManager solutionManager)
        {
            if (sourceRepositoryProvider == null)
            {
                throw new ArgumentNullException(nameof(sourceRepositoryProvider));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (solutionManager == null)
            {
                throw new ArgumentNullException(nameof(solutionManager));
            }

            SourceRepositoryProvider = sourceRepositoryProvider;
            Settings = settings;
            SolutionManager = solutionManager;
        }

        [Obsolete("Enabling and querying legacy package restore is not supported in VS 2015 RTM.")]
        public bool IsCurrentSolutionEnabledForRestore
        {
            get
            {
                if (!SolutionManager.IsSolutionOpen)
                {
                    return false;
                }

                var solutionDirectory = SolutionManager.SolutionDirectory;
                if (string.IsNullOrEmpty(solutionDirectory))
                {
                    return false;
                }

                return FileSystemUtility.FileExists(solutionDirectory, NuGetExeFile) &&
                       FileSystemUtility.FileExists(solutionDirectory, NuGetTargetsFile);
            }
        }

        [Obsolete("Enabling and querying legacy package restore is not supported in VS 2015 RTM.")]
        public void EnableCurrentSolutionForRestore(bool fromActivation)
        {
            // See comment on Obsolete attribute. This method no-ops
        }

        public virtual async Task RaisePackagesMissingEventForSolutionAsync(string solutionDirectory, CancellationToken token)
        {
            // This method is called by both Solution Opened and Solution Closed event handlers.
            // In the case of Solution Closed event, the solutionDirectory is null or empty,
            // so we won't do the unnecessary work of checking for package references.
            var missing = false;
            if (!string.IsNullOrEmpty(solutionDirectory))
            {
                var packages = await GetPackagesInSolutionAsync(solutionDirectory, token);
                missing = packages.Any(p => p.IsMissing);
            }

            if (PackagesMissingStatusChanged != null)
            {
                PackagesMissingStatusChanged(this, new PackagesMissingStatusEventArgs(missing));
            }
        }

        /// <summary>
        /// Get the missing packages in the solution given the <paramref name="solutionDirectory"></paramref>.
        /// </summary>
        /// <returns>
        /// Returns a read-only dictionary of missing package references and the corresponding project names on which
        /// each missing package is installed.
        /// </returns>
        public async Task<IEnumerable<PackageRestoreData>> GetPackagesInSolutionAsync(string solutionDirectory, CancellationToken token)
        {
            var packageReferences = await GetPackagesReferencesDictionaryAsync(token);
            return GetPackages(solutionDirectory, packageReferences);
        }

        private IEnumerable<PackageRestoreData> GetPackages(string solutionDirectory,
            Dictionary<Packaging.PackageReference, List<string>> packageReferencesDictionary)
        {
            var nuGetPackageManager = GetNuGetPackageManager(solutionDirectory);
            return GetPackages(nuGetPackageManager, packageReferencesDictionary);
        }

        private static IEnumerable<PackageRestoreData> GetPackages(NuGetPackageManager nuGetPackageManager,
            Dictionary<Packaging.PackageReference, List<string>> packageReferencesDictionary)
        {
            var packages = new List<PackageRestoreData>();

            foreach (var packageReference in packageReferencesDictionary.Keys)
            {
                var isMissing = false;
                if (!nuGetPackageManager.PackageExistsInPackagesFolder(packageReference.PackageIdentity))
                {
                    isMissing = true;
                }

                var projectNames = packageReferencesDictionary[packageReference];

                Debug.Assert(projectNames != null);
                packages.Add(new PackageRestoreData(packageReference, projectNames, isMissing));
            }

            return packages;
        }

        private async Task<Dictionary<Packaging.PackageReference, List<string>>> GetPackagesReferencesDictionaryAsync(CancellationToken token)
        {
            var packageReferencesDict = new Dictionary<Packaging.PackageReference, List<string>>(new PackageReferenceComparer());
            if (!SolutionManager.IsSolutionAvailable)
            {
                return packageReferencesDict;
            }

            foreach (var nuGetProject in SolutionManager.GetNuGetProjects())
            {
                // skip project k projects and build aware projects
                if (nuGetProject is INuGetIntegratedProject)
                {
                    continue;
                }

                var nuGetProjectName = NuGetProject.GetUniqueNameOrName(nuGetProject);
                var installedPackageReferences = await nuGetProject.GetInstalledPackagesAsync(token);
                foreach (var installedPackageReference in installedPackageReferences)
                {
                    List<string> projectNames = null;
                    if (!packageReferencesDict.TryGetValue(installedPackageReference, out projectNames))
                    {
                        projectNames = new List<string>();
                        packageReferencesDict.Add(installedPackageReference, projectNames);
                    }
                    projectNames.Add(nuGetProjectName);
                }
            }

            return packageReferencesDict;
        }

        /// <summary>
        /// Restores missing packages for the entire solution
        /// </summary>
        /// <returns></returns>
        public virtual async Task<PackageRestoreResult> RestoreMissingPackagesInSolutionAsync(string solutionDirectory,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            var packageReferencesDictionary = await GetPackagesReferencesDictionaryAsync(token);

            // When this method is called, the step to compute if a package is missing is implicit. Assume it is true
            var packages = packageReferencesDictionary.Select(p =>
            {
                Debug.Assert(p.Value != null);
                return new PackageRestoreData(p.Key, p.Value, isMissing: true);
            });

            return await RestoreMissingPackagesAsync(solutionDirectory, packages, nuGetProjectContext, new SourceCacheContext(), token);
        }

        /// <summary>
        /// Restore missing packages for a project in the solution
        /// </summary>
        /// <param name="nuGetProject"></param>
        /// <returns></returns>
        public virtual async Task<PackageRestoreResult> RestoreMissingPackagesAsync(string solutionDirectory,
            NuGetProject nuGetProject,
            INuGetProjectContext nuGetProjectContext,
            SourceCacheContext cacheContext,
            CancellationToken token)
        {
            if (nuGetProject == null)
            {
                throw new ArgumentNullException(nameof(nuGetProject));
            }
            var installedPackages = await nuGetProject.GetInstalledPackagesAsync(token);

            var nuGetProjectName = NuGetProject.GetUniqueNameOrName(nuGetProject);
            var projectNames = new[] { nuGetProjectName };

            // When this method is called, the step to compute if a package is missing is implicit. Assume it is true
            var packages = installedPackages.Select(i => new PackageRestoreData(i, projectNames, isMissing: true));

            return await RestoreMissingPackagesAsync(solutionDirectory, packages, nuGetProjectContext, cacheContext, token);
        }

        public virtual Task<PackageRestoreResult> RestoreMissingPackagesAsync(string solutionDirectory,
            IEnumerable<PackageRestoreData> packages,
            INuGetProjectContext nuGetProjectContext,
            SourceCacheContext cacheContext,
            CancellationToken token)
        {
            if (packages == null)
            {
                throw new ArgumentNullException(nameof(packages));
            }

            return RestoreMissingPackagesAsync(
                GetNuGetPackageManager(solutionDirectory),
                packages,
                nuGetProjectContext,
                cacheContext,
                token);
        }

        private NuGetPackageManager GetNuGetPackageManager(string solutionDirectory)
        {
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(solutionDirectory, Settings);
            return new NuGetPackageManager(
                SourceRepositoryProvider,
                Settings,
                packagesFolderPath);
        }

        public Task<PackageRestoreResult> RestoreMissingPackagesAsync(NuGetPackageManager nuGetPackageManager,
            IEnumerable<PackageRestoreData> packages,
            INuGetProjectContext nuGetProjectContext,
            SourceCacheContext cacheContext,
            CancellationToken token)
        {
            var packageRestoreContext = new PackageRestoreContext(nuGetPackageManager,
                packages,
                token,
                PackageRestoredEvent,
                PackageRestoreFailedEvent,
                sourceRepositories: null,
                maxNumberOfParallelTasks: PackageManagementConstants.DefaultMaxDegreeOfParallelism);

            return RestoreMissingPackagesAsync(packageRestoreContext, nuGetProjectContext, cacheContext);
        }

        /// <summary>
        /// The static method which takes in all the possible parameters
        /// </summary>
        /// <returns>Returns true if at least one of the packages needed to be restored and got restored</returns>
        /// <remarks>
        /// Best use case is 'nuget.exe restore .sln' where there is no project loaded and there is no SolutionManager.
        /// The references are obtained by parsing of solution file and by using PackagesConfigReader. In this case,
        /// you don't construct an object of PackageRestoreManager,
        /// but just the NuGetPackageManager using constructor that does not need the SolutionManager, and, optionally
        /// register to events and/or specify the source repositories
        /// </remarks>
        public static async Task<PackageRestoreResult> RestoreMissingPackagesAsync(PackageRestoreContext packageRestoreContext, INuGetProjectContext nuGetProjectContext, SourceCacheContext cacheContext)
        {
            if (packageRestoreContext == null)
            {
                throw new ArgumentNullException(nameof(packageRestoreContext));
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException(nameof(nuGetProjectContext));
            }

            ActivityCorrelationContext.StartNew();

            var missingPackages = packageRestoreContext.Packages.Where(p => p.IsMissing).ToList();
            if (!missingPackages.Any())
            {
                return new PackageRestoreResult(true);
            }

            bool packageRestoreResult = true;
            // It is possible that the dictionary passed in may not have used the PackageReferenceComparer.
            // So, just to be sure, create a hashset with the keys from the dictionary using the PackageReferenceComparer
            // Now, we are guaranteed to not restore the same package more than once
            var hashSetOfMissingPackageReferences = new HashSet<Packaging.PackageReference>(missingPackages.Select(p => p.PackageReference), new PackageReferenceComparer());

            // Before starting to restore package, set the nuGetProjectContext such that satellite files are not copied yet
            // Satellite files will be copied as a post operation. This helps restore packages in parallel
            // and not have to determine if the package is a satellite package beforehand

            if (nuGetProjectContext.PackageExtractionContext == null)
            {
                nuGetProjectContext.PackageExtractionContext = new PackageExtractionContext(new LoggerAdapter(nuGetProjectContext));
            }
            nuGetProjectContext.PackageExtractionContext.CopySatelliteFiles = false;

            packageRestoreContext.Token.ThrowIfCancellationRequested();

            var restoreResults = await ThrottledPackageRestoreAsync(hashSetOfMissingPackageReferences, packageRestoreContext, nuGetProjectContext, cacheContext);

            packageRestoreContext.Token.ThrowIfCancellationRequested();

            await ThrottledCopySatelliteFilesAsync(hashSetOfMissingPackageReferences, packageRestoreContext, nuGetProjectContext);

            packageRestoreResult &= restoreResults.All(r => r);

            if (packageRestoreResult)
            {
                packageRestoreContext.SetRestored();
            }

            return new PackageRestoreResult(packageRestoreContext.WasRestored);
        }

        /// <summary>
        /// ThrottledPackageRestoreAsync method throttles the number of tasks created to perform package restore in
        /// parallel
        /// The maximum number of parallel tasks that may be created can be specified via
        /// <paramref name="packageRestoreContext" />
        /// The method creates a ConcurrentQueue of passed in <paramref name="packageReferences" />. And, creates a
        /// fixed number of tasks
        /// that dequeue from the ConcurrentQueue and perform package restore. So, this method should pre-populate the
        /// queue and must not enqueued to by other methods
        /// </summary>
        private static Task<bool[]> ThrottledPackageRestoreAsync(HashSet<Packaging.PackageReference> packageReferences,
            PackageRestoreContext packageRestoreContext,
            INuGetProjectContext nuGetProjectContext,
            SourceCacheContext cacheContext)
        {
            var packageReferencesQueue = new ConcurrentQueue<Packaging.PackageReference>(packageReferences);
            var tasks = new List<Task<bool>>();
            for (var i = 0; i < Math.Min(packageRestoreContext.MaxNumberOfParallelTasks, packageReferences.Count); i++)
            {
                tasks.Add(Task.Run(() => PackageRestoreRunnerAsync(packageReferencesQueue, packageRestoreContext, nuGetProjectContext, cacheContext)));
            }

            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// This is the runner which dequeues package references from <paramref name="packageReferencesQueue" />, and
        /// performs package restore
        /// Note that this method should only Dequeue from the concurrent queue and not Enqueue
        /// </summary>
        private static async Task<bool> PackageRestoreRunnerAsync(ConcurrentQueue<Packaging.PackageReference> packageReferencesQueue,
            PackageRestoreContext packageRestoreContext,
            INuGetProjectContext nuGetProjectContext,
            SourceCacheContext cacheContext)
        {
            Packaging.PackageReference currentPackageReference = null;
            var restoreResult = true;
            while (packageReferencesQueue.TryDequeue(out currentPackageReference))
            {
                var result = await RestorePackageAsync(currentPackageReference, packageRestoreContext, nuGetProjectContext, cacheContext);
                restoreResult &= result;
            }

            return restoreResult;
        }

        /// <summary>
        /// ThrottledCopySatelliteFilesAsync method throttles the number of tasks created to perform copy satellite
        /// files in parallel
        /// The maximum number of parallel tasks that may be created can be specified via
        /// <paramref name="packageRestoreContext" />
        /// The method creates a ConcurrentQueue of passed in <paramref name="packageReferences" />. And, creates a
        /// fixed number of tasks
        /// that dequeue from the ConcurrentQueue and perform copying of satellite files. So, this method should
        /// pre-populate the queue and must not enqueued to by other methods
        /// </summary>
        private static Task ThrottledCopySatelliteFilesAsync(HashSet<Packaging.PackageReference> packageReferences,
            PackageRestoreContext packageRestoreContext,
            INuGetProjectContext nuGetProjectContext)
        {
            var packageReferencesQueue = new ConcurrentQueue<Packaging.PackageReference>(packageReferences);
            var tasks = new List<Task>();
            for (var i = 0; i < Math.Min(packageRestoreContext.MaxNumberOfParallelTasks, packageReferences.Count); i++)
            {
                tasks.Add(Task.Run(() => CopySatelliteFilesRunnerAsync(packageReferencesQueue, packageRestoreContext, nuGetProjectContext)));
            }

            return Task.WhenAll(tasks);
        }

        private static async Task<bool> RestorePackageAsync(Packaging.PackageReference packageReference,
            PackageRestoreContext packageRestoreContext,
            INuGetProjectContext nuGetProjectContext,
            SourceCacheContext cacheContext)
        {
            Exception exception = null;
            var restored = false;
            try
            {
                restored = await packageRestoreContext.PackageManager.RestorePackageAsync(packageReference.PackageIdentity, nuGetProjectContext, cacheContext,
                    packageRestoreContext.SourceRepositories, packageRestoreContext.Token);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            if (packageRestoreContext.PackageRestoredEvent != null)
            {
                packageRestoreContext.PackageRestoredEvent(null, new PackageRestoredEventArgs(packageReference.PackageIdentity, restored));
            }

            // PackageReferences cannot be null here
            if (exception != null)
            {
                nuGetProjectContext.Log(ProjectManagement.MessageLevel.Warning, exception.Message);
                if (packageRestoreContext.PackageRestoreFailedEvent != null)
                {
                    var packageReferenceComparer = new PackageReferenceComparer();

                    var packageRestoreData = packageRestoreContext.Packages
                        .Where(p => packageReferenceComparer.Equals(p.PackageReference, packageReference))
                        .SingleOrDefault();

                    if (packageRestoreData != null)
                    {
                        Debug.Assert(packageRestoreData.ProjectNames != null);
                        packageRestoreContext.PackageRestoreFailedEvent(null, new PackageRestoreFailedEventArgs(packageReference,
                            exception,
                            packageRestoreData.ProjectNames));
                    }
                }
            }

            return restored;
        }

        /// <summary>
        /// This is the runner which dequeues package references from <paramref name="packageReferencesQueue" />, and
        /// performs copying of satellite files
        /// Note that this method should only Dequeue from the concurrent queue and not Enqueue
        /// </summary>
        private static async Task CopySatelliteFilesRunnerAsync(ConcurrentQueue<Packaging.PackageReference> packageReferencesQueue,
            PackageRestoreContext packageRestoreContext,
            INuGetProjectContext nuGetProjectContext)
        {
            Packaging.PackageReference currentPackageReference = null;
            while (packageReferencesQueue.TryDequeue(out currentPackageReference))
            {
                var result = await packageRestoreContext.PackageManager.CopySatelliteFilesAsync(currentPackageReference.PackageIdentity, nuGetProjectContext, packageRestoreContext.Token);
            }
        }
    }
}
