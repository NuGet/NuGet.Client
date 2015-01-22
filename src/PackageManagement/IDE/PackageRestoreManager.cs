using NuGet.Client;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
        private void OnSolutionOpenedOrClosed(object sender, EventArgs e)
        {
            // We need to do the check even on Solution Closed because, let's say if the yellow Update bar
            // is showing and the user closes the solution; in that case, we want to hide the Update bar.
            CheckForMissingPackages();
        }

        private void OnNuGetProjectAdded(object sender, NuGetProjectEventArgs e)
        {
            if (IsCurrentSolutionEnabledForRestore)
            {
                EnablePackageRestore(e.NuGetProject);
            }

            CheckForMissingPackages();
        }

        protected virtual bool EnablePackageRestore(NuGetProject nuGetProject)
        {
            var installedPackages = nuGetProject.GetInstalledPackages();
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

        public virtual void CheckForMissingPackages()
        {
            // This method is called by both Solution Opened and Solution Closed event handlers.
            // In the case of Solution Closed event, the _solutionManager.IsSolutionOpen is false,
            // and so we won't do the unnecessary work of checking for package references.
            bool missing = SolutionManager.IsSolutionOpen && GetMissingPackagesForSolution().Any();
            PackagesMissingStatusChanged(this, new PackagesMissingStatusEventArgs(missing));
        }

        /// <summary>
        ///  Gets the missing packages for the entire solution
        /// </summary>
        /// <returns></returns>
        public HashSet<PackageReference> GetMissingPackagesForSolution()
        {
            var uniquePackageReferencesFromSolution = GetPackageReferencesFromSolution();
            return GetMissingPackages(uniquePackageReferencesFromSolution);
        }

        /// <summary>
        /// Gets the missing packages for the project
        /// </summary>
        /// <param name="nuGetProject"></param>
        /// <returns></returns>
        public HashSet<PackageReference> GetMissingPackagesForProject(NuGetProject nuGetProject)
        {
            var uniquePackageReferencesFromProject = nuGetProject.GetInstalledPackages();
            return GetMissingPackages(uniquePackageReferencesFromProject);
        }


        public HashSet<PackageReference> GetMissingPackages(IEnumerable<PackageReference> uniquePackageReferences)
        {
            var nuGetPackageManager = new NuGetPackageManager(SourceRepositoryProvider, Settings, SolutionManager);
            return GetMissingPackages(nuGetPackageManager, uniquePackageReferences);
        }

        public static HashSet<PackageReference> GetMissingPackages(NuGetPackageManager nuGetPackageManager,
            IEnumerable<PackageReference> uniquePackageReferences)
        {
            try
            {
                var missingPackages = new HashSet<PackageReference>(new PackageReferenceComparer());                
                var missingPackagesEnumerable =
                    uniquePackageReferences.Where(pr => !nuGetPackageManager.PackageExistsInPackagesFolder(pr.PackageIdentity));
                foreach(var package in missingPackagesEnumerable)
                {
                    missingPackages.Add(package);
                }

                return missingPackages;
            }
            catch (Exception)
            {
                // if an exception happens during the check, assume no missing packages and move on.
                // TODO : Write to NuGetProjectContext
                return new HashSet<PackageReference>(new PackageReferenceComparer());
            }
        }

        public IEnumerable<PackageReference> GetPackageReferencesFromSolution()
        {
            List<PackageReference> packageReferences = new List<PackageReference>();
            foreach(var nuGetProject in SolutionManager.GetNuGetProjects())
            {
                packageReferences.AddRange(nuGetProject.GetInstalledPackages());
            }

            return packageReferences;
        }

        private bool PackageExistsInPackagesFolder(PackagePathResolver packagesFolderPackagePathResolver, PackageReference packageReference)
        {
            var packageFilePath = Path.Combine(packagesFolderPackagePathResolver.GetInstallPath(packageReference.PackageIdentity),
                packagesFolderPackagePathResolver.GetPackageFileName(packageReference.PackageIdentity));
            return File.Exists(packageFilePath);
        }

        /// <summary>
        /// Restores missing packages for the entire solution
        /// </summary>
        /// <returns></returns>
        public async virtual Task RestoreMissingPackages()
        {
            var missingPackages = GetMissingPackagesForSolution();
            if(missingPackages.Count > 0)
            {
                await RestoreMissingPackages(missingPackages);
            }            
        }

        /// <summary>
        /// Restore missing packages for a project in the solution
        /// </summary>
        /// <param name="nuGetProject"></param>
        /// <returns></returns>
        public async virtual Task RestoreMissingPackages(NuGetProject nuGetProject)
        {
            var missingPackages = GetMissingPackagesForProject(nuGetProject);
            if (missingPackages.Count > 0)
            {
                await RestoreMissingPackages(missingPackages);
            }
        }

        public async virtual Task RestoreMissingPackages(HashSet<PackageReference> missingPackages)
        {
            var nuGetPackageManager = new NuGetPackageManager(SourceRepositoryProvider, Settings, SolutionManager);
            await RestoreMissingPackages(nuGetPackageManager, missingPackages, SolutionManager.NuGetProjectContext ?? new EmptyNuGetProjectContext());
        }

        public static async Task RestoreMissingPackages(NuGetPackageManager nuGetPackageManager,
            HashSet<PackageReference> missingPackages,
            INuGetProjectContext nuGetProjectContext)
        {
            // TODO: Update this to use the locked version
            await Task.WhenAll(missingPackages.Select(missingPackage =>
                nuGetPackageManager.RestorePackage(missingPackage.PackageIdentity,
                nuGetProjectContext)));
        }
    }
}
