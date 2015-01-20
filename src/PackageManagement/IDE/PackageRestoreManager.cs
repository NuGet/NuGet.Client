using NuGet.Client;
using NuGet.Configuration;
using NuGet.Packaging;
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

        protected SourceRepositoryProvider SourceRepositoryProvider { get; set; }
        protected ISolutionManager SolutionManager { get; set; }
        protected ISettings Settings { get; set; }
        public PackageRestoreManager(SourceRepositoryProvider sourceRepositoryProvider, ISolutionManager solutionManager, ISettings settings)
        {
            if(sourceRepositoryProvider == null)
            {
                throw new ArgumentNullException("sourceRepositoryProvider");
            }

            if(solutionManager == null)
            {
                throw new ArgumentNullException("solutionManager");
            }

            if(settings == null)
            {
                throw new ArgumentNullException("settings");
            }

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
            bool missing = SolutionManager.IsSolutionOpen && GetMissingPackagesCore().Any();
            PackagesMissingStatusChanged(this, new PackagesMissingStatusEventArgs(missing));
        }

        private IEnumerable<PackageReference> GetMissingPackagesCore()
        {
            try
            {
                var nuGetPackageManager = new NuGetPackageManager(SourceRepositoryProvider, Settings, SolutionManager);
                var packagesFolderPackagePathResolver = nuGetPackageManager.PackagePathResolver;
                var allPackageReferences = GetAllPackageReferences(SolutionManager);
                return allPackageReferences.Where(pr => !PackageExistsInPackagesFolder(packagesFolderPackagePathResolver, pr));
            }
            catch (Exception)
            {
                // if an exception happens during the check, assume no missing packages and move on.
                // TODO : Write to NuGetProjectContext
                return Enumerable.Empty<PackageReference>();
            }
        }

        private IEnumerable<PackageReference> GetAllPackageReferences(ISolutionManager solutionManager)
        {
            List<PackageReference> allPackageReferences = new List<PackageReference>();
            foreach(var nuGetProject in SolutionManager.GetNuGetProjects())
            {
                allPackageReferences.AddRange(nuGetProject.GetInstalledPackages());
            }

            return allPackageReferences;
        }

        private bool PackageExistsInPackagesFolder(PackagePathResolver packagesFolderPackagePathResolver, PackageReference packageReference)
        {
            return File.Exists(packagesFolderPackagePathResolver.GetPackageFileName(packageReference.PackageIdentity));
        }

        public async virtual Task RestoreMissingPackages()
        {
            var nuGetPackageManager = new NuGetPackageManager(SourceRepositoryProvider, Settings, SolutionManager);
            var folderNuGetProject = new FolderNuGetProject(nuGetPackageManager.PackagesFolderPath);
            var resolutionContext = new ResolutionContext(Resolver.DependencyBehavior.Ignore, includePrelease: true);
            var missingPackages = GetMissingPackagesCore();
            await Task.WhenAll(missingPackages.Select(missingPackageReference =>
                nuGetPackageManager.InstallPackageAsync(folderNuGetProject, missingPackageReference.PackageIdentity,
                resolutionContext, SolutionManager.NuGetProjectContext ?? new EmptyNuGetProjectContext())));
        }
    }
}
