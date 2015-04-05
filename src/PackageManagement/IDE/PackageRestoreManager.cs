using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    public class PackageRestoreManager : IPackageRestoreManager
    {
        private const string NuGetSolutionSettingsFolder = ".nuget";
        private static readonly string NuGetExeFile = Path.Combine(NuGetSolutionSettingsFolder, "NuGet.exe");
        private static readonly string NuGetTargetsFile = Path.Combine(NuGetSolutionSettingsFolder, "NuGet.targets");

        private ISourceRepositoryProvider SourceRepositoryProvider { get; set; }
        private ISolutionManager SolutionManager { get; set; }
        private ISettings Settings { get; set; }

        public event EventHandler<PackagesMissingStatusEventArgs> PackagesMissingStatusChanged;
        public event EventHandler<PackageRestoredEventArgs> PackageRestoredEvent;
        public event EventHandler<PackageRestoreFailedEventArgs> PackageRestoreFailedEvent;

        public PackageRestoreManager(ISourceRepositoryProvider sourceRepositoryProvider, ISettings settings, ISolutionManager solutionManager)
        {
            if (sourceRepositoryProvider == null)
            {
                throw new ArgumentNullException("sourceRepositoryProvider");
            }

            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            if (solutionManager == null)
            {
                throw new ArgumentNullException("solutionManager");
            }

            SourceRepositoryProvider = sourceRepositoryProvider;
            Settings = settings;
            SolutionManager = solutionManager;


            SolutionManager.NuGetProjectAdded += OnNuGetProjectAdded;
            SolutionManager.SolutionOpened += OnSolutionOpenedOrClosed;
            SolutionManager.SolutionClosed += OnSolutionOpenedOrClosed;
        }
        private async void OnSolutionOpenedOrClosed(object sender, EventArgs e)
        {
            // We need to do the check even on Solution Closed because, let's say if the yellow Update bar
            // is showing and the user closes the solution; in that case, we want to hide the Update bar.
            var solutionDirectory = SolutionManager.SolutionDirectory;
            await RaisePackagesMissingEventForSolutionAsync(solutionDirectory, CancellationToken.None);
        }

        private async void OnNuGetProjectAdded(object sender, NuGetProjectEventArgs e)
        {
            var solutionDirectory = SolutionManager.SolutionDirectory;
            await RaisePackagesMissingEventForSolutionAsync(solutionDirectory, CancellationToken.None);
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

                string solutionDirectory = SolutionManager.SolutionDirectory;
                if (String.IsNullOrEmpty(solutionDirectory))
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

        public async virtual Task RaisePackagesMissingEventForSolutionAsync(string solutionDirectory, CancellationToken token)
        {
            // This method is called by both Solution Opened and Solution Closed event handlers.
            // In the case of Solution Closed event, the solutionDirectory is null or empty,
            // so we won't do the unnecessary work of checking for package references.
            bool missing = false;
            if (!String.IsNullOrEmpty(solutionDirectory))
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
        /// <returns>Returns a read-only dictionary of missing package references and the corresponding project names on which each missing package is installed.
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
                // skip project k projects
                if (nuGetProject is ProjectManagement.Projects.ProjectKNuGetProjectBase)
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
        public async virtual Task<bool> RestoreMissingPackagesInSolutionAsync(string solutionDirectory, CancellationToken token)
        {
            var packageReferencesFromSolution = await GetPackagesInfoSolutionAsync(token);
            return await RestoreMissingPackagesAsync(solutionDirectory, packageReferencesFromSolution, token);
        }

        /// <summary>
        /// Restore missing packages for a project in the solution
        /// </summary>
        /// <param name="nuGetProject"></param>
        /// <returns></returns>
        public async virtual Task<bool> RestoreMissingPackagesAsync(string solutionDirectory, NuGetProject nuGetProject, CancellationToken token)
        {
            if (nuGetProject == null)
            {
                throw new ArgumentNullException("nuGetProject");
            }
            var installedPackages = await nuGetProject.GetInstalledPackagesAsync(token);

            var nuGetProjectName = NuGetProject.GetUniqueNameOrName(nuGetProject);
            IReadOnlyCollection<string> projectNames = new string[] { nuGetProjectName };
            var packageReferencesDict = installedPackages.ToDictionary(i => i, i => projectNames);
            var missingPackagesInfo = new MissingPackagesInfo(packageReferencesDict);

            return await RestoreMissingPackagesAsync(solutionDirectory, missingPackagesInfo, token);
        }

        public async virtual Task<bool> RestoreMissingPackagesAsync(string solutionDirectory,
            MissingPackagesInfo missingPackagesInfo,
            CancellationToken token)
        {
            if (missingPackagesInfo == null)
            {
                throw new ArgumentNullException("packageReferences");
            }

            var nuGetPackageManager = GetNuGetPackageManager(solutionDirectory);
            return await RestoreMissingPackagesAsync(nuGetPackageManager, missingPackagesInfo,
                SolutionManager.NuGetProjectContext ?? new EmptyNuGetProjectContext(), token, PackageRestoredEvent, PackageRestoreFailedEvent);
        }

        private NuGetPackageManager GetNuGetPackageManager(string solutionDirectory)
        {
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(solutionDirectory, Settings);
            var nuGetPackageManager = new NuGetPackageManager(SourceRepositoryProvider, packagesFolderPath);
            return nuGetPackageManager;
        }

        public async Task<bool> RestoreMissingPackagesAsync(NuGetPackageManager nuGetPackageManager,
            MissingPackagesInfo missingPackagesInfo,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            return await RestoreMissingPackagesAsync(nuGetPackageManager, missingPackagesInfo, nuGetProjectContext, token,
                PackageRestoredEvent, PackageRestoreFailedEvent);
        }

        /// <summary>
        /// The static method which takes in all the possible parameters
        /// </summary>
        /// <remarks>Best use case is 'nuget.exe restore .sln' where there is no project loaded and there is no SolutionManager. The references are obtained by parsing of solution file and by using PackagesConfigReader. In this case, you don't construct an object of PackageRestoreManager, but just the NuGetPackageManager using constructor that does not need the SolutionManager, and, optionally register to events and/or specify the source repositories </remarks>
        public static async Task<bool> RestoreMissingPackagesAsync(NuGetPackageManager nuGetPackageManager,
            MissingPackagesInfo missingPackagesInfo,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token,
            EventHandler<PackageRestoredEventArgs> packageRestoredEvent = null,
            EventHandler<PackageRestoreFailedEventArgs> packageRestoreFailedEvent = null,
            IEnumerable<SourceRepository> sourceRepositories = null)
        {
            if (nuGetPackageManager == null)
            {
                throw new ArgumentNullException("nuGetPackageManager");
            }

            if (missingPackagesInfo == null)
            {
                throw new ArgumentNullException("packageReferences");
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException("nuGetProjectContext");
            }

            if (!missingPackagesInfo.PackageReferences.Any())
                return false;

            // It is possible that the dictionary passed in may not have used the PackageReferenceComparer.
            // So, just to be sure, create a hashset with the keys from the dictionary using the PackageReferenceComparer
            // Now, we are guaranteed to not restore the same package more than once
            var hashSetOfMissingPackageReferences = new HashSet<PackageReference>(missingPackagesInfo.PackageReferences.Keys, new PackageReferenceComparer());

            // Before starting to restore package, set the nuGetProjectContext such that satellite files are not copied yet
            // Satellite files will be copied as a post operation. This helps restore packages in parallel
            // and not have to determine if the package is a satellite package beforehand

            if (nuGetProjectContext.PackageExtractionContext == null)
            {
                nuGetProjectContext.PackageExtractionContext = new PackageExtractionContext();
            }
            nuGetProjectContext.PackageExtractionContext.CopySatelliteFiles = false;

            token.ThrowIfCancellationRequested();

            bool[] results = await Task.WhenAll(hashSetOfMissingPackageReferences.Select(uniqueMissingPackage =>
                RestorePackageAsync(nuGetPackageManager, missingPackagesInfo, uniqueMissingPackage, nuGetProjectContext,
                packageRestoredEvent, packageRestoreFailedEvent, sourceRepositories, token)));

            token.ThrowIfCancellationRequested();
            bool[] satelliteFileResults = await Task.WhenAll(hashSetOfMissingPackageReferences.Select(uniqueMissingPackage =>
                nuGetPackageManager.CopySatelliteFilesAsync(uniqueMissingPackage.PackageIdentity, nuGetProjectContext, token)));

            return results.Any() || satelliteFileResults.Any();
        }

        private static async Task<bool> RestorePackageAsync(NuGetPackageManager nuGetPackageManager,
            MissingPackagesInfo missingPackagesInfo,
            PackageReference packageReference,
            INuGetProjectContext nuGetProjectContext,
            EventHandler<PackageRestoredEventArgs> packageRestoredEvent,
            EventHandler<PackageRestoreFailedEventArgs> packageRestoreFailedEvent,
            IEnumerable<SourceRepository> sourceRepositories,
            CancellationToken token)
        {
            Exception exception = null;
            bool restored = false;
            try
            {
                restored = await nuGetPackageManager.RestorePackageAsync(packageReference.PackageIdentity, nuGetProjectContext, sourceRepositories, token);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            if (packageRestoredEvent != null)
            {
                packageRestoredEvent(null, new PackageRestoredEventArgs(packageReference.PackageIdentity, restored));
            }

            // PackageReferences cannot be null here
            if (exception != null)
            {
                nuGetProjectContext.Log(MessageLevel.Warning, exception.ToString());
                if(packageRestoreFailedEvent != null && missingPackagesInfo.PackageReferences.ContainsKey(packageReference))
                {
                    packageRestoreFailedEvent(null, new PackageRestoreFailedEventArgs(packageReference,
                        exception,
                        missingPackagesInfo.PackageReferences[packageReference]));
                }
            }

            return restored;
        }
    }
}
