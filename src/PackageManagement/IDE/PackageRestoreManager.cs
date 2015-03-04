using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    public class PackageRestoreManager : IPackageRestoreManager
    {
        public const string NuGetSolutionSettingsFolder = ".nuget";
        protected static readonly string NuGetExeFile = Path.Combine(NuGetSolutionSettingsFolder, "NuGet.exe");
        protected static readonly string NuGetTargetsFile = Path.Combine(NuGetSolutionSettingsFolder, "NuGet.targets");
        protected const string NuGetBuildPackageName = "NuGet.Build";
        protected const string NuGetCommandLinePackageName = "NuGet.CommandLine";

        protected ISourceRepositoryProvider SourceRepositoryProvider { get; set; }
        protected ISolutionManager SolutionManager { get; set; }
        protected ISettings Settings { get; set; }
        public PackageRestoreManager(ISourceRepositoryProvider sourceRepositoryProvider, ISettings settings, ISolutionManager solutionManager)
        {
            if(sourceRepositoryProvider == null)
            {
                throw new ArgumentNullException("sourceRepositoryProvider");
            }

            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            if(solutionManager == null)
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
            await RaisePackagesMissingEventForSolution(CancellationToken.None);
        }

        private async void OnNuGetProjectAdded(object sender, NuGetProjectEventArgs e)
        {
            if (IsCurrentSolutionEnabledForRestore)
            {
                EnablePackageRestore(e.NuGetProject);
            }

            await RaisePackagesMissingEventForSolution(CancellationToken.None);
        }

        protected virtual bool EnablePackageRestore(NuGetProject nuGetProject)
        {
            var installedPackages = nuGetProject.GetInstalledPackagesAsync(CancellationToken.None).Result;
            if(!installedPackages.Any())
            {
                return true;
            }

            return false;
        }

        public virtual bool IsCurrentSolutionEnabledForRestore
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

        public virtual void EnableCurrentSolutionForRestore(bool fromActivation)
        {
            throw new NotImplementedException();
        }

        public event EventHandler<PackagesMissingStatusEventArgs> PackagesMissingStatusChanged;

        public async virtual Task RaisePackagesMissingEventForSolution(CancellationToken token)
        {
            // This method is called by both Solution Opened and Solution Closed event handlers.
            // In the case of Solution Closed event, the _solutionManager.IsSolutionOpen is false,
            // and so we won't do the unnecessary work of checking for package references.
            bool missing = SolutionManager.IsSolutionOpen && (await GetMissingPackagesInSolution(token)).Any();

            if (PackagesMissingStatusChanged != null)
            {
                PackagesMissingStatusChanged(this, new PackagesMissingStatusEventArgs(missing));
            }
        }

        /// <summary>
        ///  Gets the missing packages for the entire solution
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<PackageReference>> GetMissingPackagesInSolution(CancellationToken token)
        {
            var packageReferencesFromSolution = await GetPackageReferencesFromSolution(token);
            return GetMissingPackages(packageReferencesFromSolution);
        }

        public async Task<IEnumerable<PackageReference>> GetMissingPackages(NuGetProject nuGetProject, CancellationToken token)
        {
            return GetMissingPackages(await nuGetProject.GetInstalledPackagesAsync(token));
        }

        public IEnumerable<PackageReference> GetMissingPackages(IEnumerable<PackageReference> packageReferences)
        {
            var nuGetPackageManager = new NuGetPackageManager(SourceRepositoryProvider, Settings, SolutionManager);
            return GetMissingPackages(nuGetPackageManager, packageReferences);
        }

        public static IEnumerable<PackageReference> GetMissingPackages(NuGetPackageManager nuGetPackageManager,
            IEnumerable<PackageReference> packageReferences)
        {
            try
            {
                return new HashSet<PackageReference>(packageReferences.Where(pr => !nuGetPackageManager.PackageExistsInPackagesFolder(pr.PackageIdentity)),
                    new PackageReferenceComparer());
            }
            catch (Exception)
            {
                // if an exception happens during the check, assume no missing packages and move on.
                // TODO : Write to NuGetProjectContext
                return Enumerable.Empty<PackageReference>();
            }
        }

        public async Task<IEnumerable<PackageReference>> GetPackageReferencesFromSolution(CancellationToken token)
        {
            List<PackageReference> packageReferences = new List<PackageReference>();
            foreach(var nuGetProject in SolutionManager.GetNuGetProjects())
            {
                // skip project k projects
                if (nuGetProject is ProjectManagement.Projects.ProjectKNuGetProjectBase)
                {
                    continue;
                }

                packageReferences.AddRange(await nuGetProject.GetInstalledPackagesAsync(token));
            }

            return packageReferences;
        }

        /// <summary>
        /// Restores missing packages for the entire solution
        /// </summary>
        /// <returns></returns>
        public async virtual Task<bool> RestoreMissingPackagesInSolutionAsync(CancellationToken token)
        {
            return await RestoreMissingPackagesAsync(await GetPackageReferencesFromSolution(token), token);
        }

        /// <summary>
        /// Restore missing packages for a project in the solution
        /// </summary>
        /// <param name="nuGetProject"></param>
        /// <returns></returns>
        public async virtual Task<bool> RestoreMissingPackagesAsync(NuGetProject nuGetProject, CancellationToken token)
        {
            if(nuGetProject == null)
            {
                throw new ArgumentNullException("nuGetProject");
            }
            return await RestoreMissingPackagesAsync(await nuGetProject.GetInstalledPackagesAsync(token), token);
        }

        public async virtual Task<bool> RestoreMissingPackagesAsync(IEnumerable<PackageReference> packageReferences, CancellationToken token)
        {
            if(packageReferences == null)
            {
                throw new ArgumentNullException("packageReferences");
            }

            var nuGetPackageManager = new NuGetPackageManager(SourceRepositoryProvider, Settings, SolutionManager);
            return await RestoreMissingPackages(nuGetPackageManager, packageReferences,
                SolutionManager.NuGetProjectContext ?? new EmptyNuGetProjectContext(), token, PackageRestoredEvent);
        }

        public static async Task<bool> RestoreMissingPackages(NuGetPackageManager nuGetPackageManager,
            IEnumerable<PackageReference> packageReferences,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token,
            EventHandler<PackageRestoredEventArgs> packageRestoredEvent = null,
            IEnumerable<SourceRepository> sourceRepositories = null)
        {
            if(nuGetPackageManager == null)
            {
                throw new ArgumentNullException("nuGetPackageManager");
            }

            if(packageReferences == null)
            {
                throw new ArgumentNullException("packageReferences");
            }

            if(nuGetProjectContext == null)
            {
                throw new ArgumentNullException("nuGetProjectContext");
            }

            if (!packageReferences.Any())
                return false;

            var hashSetOfMissingPackageReferences = new HashSet<PackageReference>(packageReferences, new PackageReferenceComparer()); 

            // Before starting to restore package, set the nuGetProjectContext such that satellite files are not copied yet
            // Satellite files will be copied as a post operation. This helps restore packages in parallel
            // and not have to determine if the package is a satellite package beforehand

            if(nuGetProjectContext.PackageExtractionContext == null)
            {
                nuGetProjectContext.PackageExtractionContext = new PackageExtractionContext();
            }
            nuGetProjectContext.PackageExtractionContext.CopySatelliteFiles = false;

            token.ThrowIfCancellationRequested();
            // TODO: Update this to use the locked version
            bool[] results = await Task.WhenAll(hashSetOfMissingPackageReferences.Select(uniqueMissingPackage =>
                RestorePackageAsync(nuGetPackageManager, uniqueMissingPackage.PackageIdentity, nuGetProjectContext,
                packageRestoredEvent, sourceRepositories, token)));

            token.ThrowIfCancellationRequested();
            bool[] satelliteFileResults = await Task.WhenAll(hashSetOfMissingPackageReferences.Select(uniqueMissingPackage =>
                nuGetPackageManager.CopySatelliteFilesAsync(uniqueMissingPackage.PackageIdentity, nuGetProjectContext, token)));

            return results.Any() || satelliteFileResults.Any();
        }

        private static async Task<bool> RestorePackageAsync(NuGetPackageManager nuGetPackageManager,
            PackageIdentity packageIdentity,
            INuGetProjectContext nuGetProjectContext,
            EventHandler<PackageRestoredEventArgs> packageRestoredEvent,
            IEnumerable<SourceRepository> sourceRepositories,
            CancellationToken token)
        {
            bool restored = await nuGetPackageManager.RestorePackageAsync(packageIdentity, nuGetProjectContext, sourceRepositories, token);
            // At this point, it is guaranteed that package restore did not fail
            if(packageRestoredEvent != null)
            {
                packageRestoredEvent(null, new PackageRestoredEventArgs(packageIdentity, restored));
            }

            return restored;
        }


        public event EventHandler<PackageRestoredEventArgs> PackageRestoredEvent;
    }
}
