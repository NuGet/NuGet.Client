// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private ISettings Settings { get; }

        public event EventHandler<PackagesMissingStatusEventArgs> PackagesMissingStatusChanged;
        public event EventHandler<PackageRestoredEventArgs> PackageRestoredEvent;
        public event EventHandler<PackageRestoreFailedEventArgs> PackageRestoreFailedEvent;

        public PackageRestoreManager(ISourceRepositoryProvider sourceRepositoryProvider, ISettings settings, ISolutionManager solutionManager)
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
                var missingPackagesInfo = await GetMissingPackagesInSolutionAsync(solutionDirectory, token);
                missing = missingPackagesInfo.PackageReferences.Any();
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
        public async Task<MissingPackagesInfo> GetMissingPackagesInSolutionAsync(string solutionDirectory, CancellationToken token)
        {
            var packagesInfo = await GetPackagesInfoSolutionAsync(token);
            var missingPackagesInfo = GetMissingPackages(solutionDirectory, packagesInfo);
            return missingPackagesInfo;
        }

        private MissingPackagesInfo GetMissingPackages(string solutionDirectory,
            MissingPackagesInfo packagesInfo)
        {
            var nuGetPackageManager = GetNuGetPackageManager(solutionDirectory);
            var missingPackagesInfo = GetMissingPackages(nuGetPackageManager, packagesInfo);
            return missingPackagesInfo;
        }

        private static MissingPackagesInfo GetMissingPackages(NuGetPackageManager nuGetPackageManager,
            MissingPackagesInfo packagesInfo)
        {
            try
            {
                var availablePackageReferences = packagesInfo.PackageReferences.Keys.Where(pr => nuGetPackageManager.PackageExistsInPackagesFolder(pr.PackageIdentity)).ToList();
                foreach (var availablePackageReference in availablePackageReferences)
                {
                    packagesInfo.InternalPackageReferences.Remove(availablePackageReference);
                }

                // Removed the available packages from packagesInfo. So, it is the missingPackagesInfo
                return packagesInfo;
            }
            catch (Exception)
            {
                // if an exception happens during the check, assume no missing packages and move on.
                return MissingPackagesInfo.Empty;
            }
        }

        public async Task<MissingPackagesInfo> GetPackagesInfoSolutionAsync(CancellationToken token)
        {
            var packageReferencesDict = new Dictionary<PackageReference, HashSet<string>>(new PackageReferenceComparer());
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
                    HashSet<string> projectNames = null;
                    if (!packageReferencesDict.TryGetValue(installedPackageReference, out projectNames))
                    {
                        projectNames = new HashSet<string>();
                        packageReferencesDict.Add(installedPackageReference, projectNames);
                    }
                    projectNames.Add(nuGetProjectName);
                }
            }

            var readOnlyPackageReferencesDict = new Dictionary<PackageReference, IReadOnlyCollection<string>>(new PackageReferenceComparer());
            foreach (var item in packageReferencesDict)
            {
                readOnlyPackageReferencesDict.Add(item.Key, (IReadOnlyCollection<string>)item.Value);
            }

            var missingPackagesInfo = new MissingPackagesInfo(readOnlyPackageReferencesDict);
            return missingPackagesInfo;
        }

        /// <summary>
        /// Restores missing packages for the entire solution
        /// </summary>
        /// <returns></returns>
        public virtual async Task<PackageRestoreResult> RestoreMissingPackagesInSolutionAsync(string solutionDirectory, CancellationToken token)
        {
            var packageReferencesFromSolution = await GetPackagesInfoSolutionAsync(token);
            return await RestoreMissingPackagesAsync(solutionDirectory, packageReferencesFromSolution, token);
        }

        /// <summary>
        /// Restore missing packages for a project in the solution
        /// </summary>
        /// <param name="nuGetProject"></param>
        /// <returns></returns>
        public virtual async Task<PackageRestoreResult> RestoreMissingPackagesAsync(string solutionDirectory,
            NuGetProject nuGetProject,
            CancellationToken token)
        {
            if (nuGetProject == null)
            {
                throw new ArgumentNullException("nuGetProject");
            }
            var installedPackages = await nuGetProject.GetInstalledPackagesAsync(token);

            var nuGetProjectName = NuGetProject.GetUniqueNameOrName(nuGetProject);
            IReadOnlyCollection<string> projectNames = new[] { nuGetProjectName };
            var packageReferencesDict = installedPackages.ToDictionary(i => i, i => projectNames);
            var missingPackagesInfo = new MissingPackagesInfo(packageReferencesDict);

            return await RestoreMissingPackagesAsync(solutionDirectory, missingPackagesInfo, token);
        }

        public virtual async Task<PackageRestoreResult> RestoreMissingPackagesAsync(string solutionDirectory,
            MissingPackagesInfo missingPackagesInfo,
            CancellationToken token)
        {
            if (missingPackagesInfo == null)
            {
                throw new ArgumentNullException(nameof(missingPackagesInfo));
            }

            var nuGetPackageManager = GetNuGetPackageManager(solutionDirectory);
            var packageRestoreContext = new PackageRestoreContext(nuGetPackageManager,
                missingPackagesInfo,
                token,
                PackageRestoredEvent,
                PackageRestoreFailedEvent,
                sourceRepositories: null,
                maxNumberOfParallelTasks: PackageRestoreContext.DefaultMaxNumberOfParellelTasks);

            return await RestoreMissingPackagesAsync(packageRestoreContext, SolutionManager.NuGetProjectContext ?? new EmptyNuGetProjectContext());
        }

        private NuGetPackageManager GetNuGetPackageManager(string solutionDirectory)
        {
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(solutionDirectory, Settings);
            var nuGetPackageManager = new NuGetPackageManager(SourceRepositoryProvider, packagesFolderPath);
            return nuGetPackageManager;
        }

        public async Task<PackageRestoreResult> RestoreMissingPackagesAsync(NuGetPackageManager nuGetPackageManager,
            MissingPackagesInfo missingPackagesInfo,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            var packageRestoreContext = new PackageRestoreContext(nuGetPackageManager,
                missingPackagesInfo,
                token,
                PackageRestoredEvent,
                PackageRestoreFailedEvent,
                sourceRepositories: null,
                maxNumberOfParallelTasks: PackageRestoreContext.DefaultMaxNumberOfParellelTasks);

            return await RestoreMissingPackagesAsync(packageRestoreContext, nuGetProjectContext);
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
        public static async Task<PackageRestoreResult> RestoreMissingPackagesAsync(PackageRestoreContext packageRestoreContext, INuGetProjectContext nuGetProjectContext)
        {
            if (packageRestoreContext == null)
            {
                throw new ArgumentNullException(nameof(packageRestoreContext));
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException(nameof(nuGetProjectContext));
            }

            if (!packageRestoreContext.MissingPackagesInfo.PackageReferences.Any())
            {
                return new PackageRestoreResult(false);
            }

            // It is possible that the dictionary passed in may not have used the PackageReferenceComparer.
            // So, just to be sure, create a hashset with the keys from the dictionary using the PackageReferenceComparer
            // Now, we are guaranteed to not restore the same package more than once
            var hashSetOfMissingPackageReferences = new HashSet<PackageReference>(packageRestoreContext.MissingPackagesInfo.PackageReferences.Keys, new PackageReferenceComparer());

            // Before starting to restore package, set the nuGetProjectContext such that satellite files are not copied yet
            // Satellite files will be copied as a post operation. This helps restore packages in parallel
            // and not have to determine if the package is a satellite package beforehand

            if (nuGetProjectContext.PackageExtractionContext == null)
            {
                nuGetProjectContext.PackageExtractionContext = new PackageExtractionContext();
            }
            nuGetProjectContext.PackageExtractionContext.CopySatelliteFiles = false;

            packageRestoreContext.Token.ThrowIfCancellationRequested();

            await ThrottledPackageRestoreAsync(hashSetOfMissingPackageReferences, packageRestoreContext, nuGetProjectContext);

            packageRestoreContext.Token.ThrowIfCancellationRequested();

            await ThrottledCopySatelliteFilesAsync(hashSetOfMissingPackageReferences, packageRestoreContext, nuGetProjectContext);

            var result = new PackageRestoreResult(packageRestoreContext.WasRestored);
            return result;
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
        private static Task ThrottledPackageRestoreAsync(HashSet<PackageReference> packageReferences,
            PackageRestoreContext packageRestoreContext,
            INuGetProjectContext nuGetProjectContext)
        {
            var packageReferencesQueue = new ConcurrentQueue<PackageReference>(packageReferences);
            var tasks = new List<Task>();
            for (var i = 0; i < Math.Min(packageRestoreContext.MaxNumberOfParallelTasks, packageReferences.Count); i++)
            {
                tasks.Add(Task.Run(() => PackageRestoreRunnerAsync(packageReferencesQueue, packageRestoreContext, nuGetProjectContext)));
            }

            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// This is the runner which dequeues package references from <paramref name="packageReferencesQueue" />, and
        /// performs package restore
        /// Note that this method should only Dequeue from the concurrent queue and not Enqueue
        /// </summary>
        private static async Task PackageRestoreRunnerAsync(ConcurrentQueue<PackageReference> packageReferencesQueue,
            PackageRestoreContext packageRestoreContext,
            INuGetProjectContext nuGetProjectContext)
        {
            PackageReference currentPackageReference = null;
            while (packageReferencesQueue.TryDequeue(out currentPackageReference))
            {
                var result = await RestorePackageAsync(currentPackageReference, packageRestoreContext, nuGetProjectContext);
                if (result)
                {
                    packageRestoreContext.SetRestored();
                }
            }
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
        private static Task ThrottledCopySatelliteFilesAsync(HashSet<PackageReference> packageReferences,
            PackageRestoreContext packageRestoreContext,
            INuGetProjectContext nuGetProjectContext)
        {
            var packageReferencesQueue = new ConcurrentQueue<PackageReference>(packageReferences);
            var tasks = new List<Task>();
            for (var i = 0; i < Math.Min(packageRestoreContext.MaxNumberOfParallelTasks, packageReferences.Count); i++)
            {
                tasks.Add(Task.Run(() => CopySatelliteFilesRunnerAsync(packageReferencesQueue, packageRestoreContext, nuGetProjectContext)));
            }

            return Task.WhenAll(tasks);
        }

        private static async Task<bool> RestorePackageAsync(PackageReference packageReference,
            PackageRestoreContext packageRestoreContext,
            INuGetProjectContext nuGetProjectContext)
        {
            Exception exception = null;
            var restored = false;
            try
            {
                restored = await packageRestoreContext.PackageManager.RestorePackageAsync(packageReference.PackageIdentity, nuGetProjectContext,
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
                nuGetProjectContext.Log(MessageLevel.Warning, exception.ToString());
                if (packageRestoreContext.PackageRestoreFailedEvent != null
                    && packageRestoreContext.MissingPackagesInfo.PackageReferences.ContainsKey(packageReference))
                {
                    packageRestoreContext.PackageRestoreFailedEvent(null, new PackageRestoreFailedEventArgs(packageReference,
                        exception,
                        packageRestoreContext.MissingPackagesInfo.PackageReferences[packageReference]));
                }
            }

            return restored;
        }

        /// <summary>
        /// This is the runner which dequeues package references from <paramref name="packageReferencesQueue" />, and
        /// performs copying of satellite files
        /// Note that this method should only Dequeue from the concurrent queue and not Enqueue
        /// </summary>
        private static async Task CopySatelliteFilesRunnerAsync(ConcurrentQueue<PackageReference> packageReferencesQueue,
            PackageRestoreContext packageRestoreContext,
            INuGetProjectContext nuGetProjectContext)
        {
            PackageReference currentPackageReference = null;
            while (packageReferencesQueue.TryDequeue(out currentPackageReference))
            {
                var result = await packageRestoreContext.PackageManager.CopySatelliteFilesAsync(currentPackageReference.PackageIdentity, nuGetProjectContext, packageRestoreContext.Token);
                if (result)
                {
                    packageRestoreContext.SetRestored();
                }
            }
        }
    }
}
