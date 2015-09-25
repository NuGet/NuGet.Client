using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using NuGet.Configuration;

namespace NuGet.Common
{
    /// <summary>
    /// Handles updating the executing instance of NuGet.exe
    /// </summary>
    public class SelfUpdater
    {
        private const string NuGetCommandLinePackageId = "NuGet.CommandLine";
        private const string NuGetExe = "NuGet.exe";

        private readonly IPackageRepositoryFactory _repositoryFactory;

        public SelfUpdater(IPackageRepositoryFactory repositoryFactory)
        {
            if (repositoryFactory == null)
            {
                throw new ArgumentNullException("repositoryFactory");
            }
            _repositoryFactory = repositoryFactory;
        }

        public IConsole Console { get; set; }

        public void UpdateSelf()
        {
            Assembly assembly = typeof(SelfUpdater).Assembly;
            var version = GetNuGetVersion(assembly) ?? new SemanticVersion(assembly.GetName().Version);
            SelfUpdate(assembly.Location, version);
        }

        internal void SelfUpdate(string exePath, SemanticVersion version)
        {
            Console.WriteLine(LocalizedResourceManager.GetString("UpdateCommandCheckingForUpdates"), NuGetConstants.V2FeedUrl);

            // Get the nuget command line package from the specified repository
            IPackageRepository packageRepository = _repositoryFactory.CreateRepository(NuGetConstants.V2FeedUrl);
            IPackage package = packageRepository.GetUpdates(
                new [] { new PackageName(NuGetCommandLinePackageId, version) },
                includePrerelease: true, 
                includeAllVersions: false, 
                targetFrameworks: null,
                versionConstraints: null).FirstOrDefault();
 
            Console.WriteLine(LocalizedResourceManager.GetString("UpdateCommandCurrentlyRunningNuGetExe"), version);

            // Check to see if an update is needed
            if (package == null || version >= package.Version)
            {
                Console.WriteLine(LocalizedResourceManager.GetString("UpdateCommandNuGetUpToDate"));
            }
            else
            {
                Console.WriteLine(LocalizedResourceManager.GetString("UpdateCommandUpdatingNuGet"), package.Version);

                // Get NuGet.exe file from the package
                IPackageFile file = package.GetFiles().FirstOrDefault(f => Path.GetFileName(f.Path).Equals(NuGetExe, StringComparison.OrdinalIgnoreCase));

                // If for some reason this package doesn't have NuGet.exe then we don't want to use it
                if (file == null)
                {
                    throw new CommandLineException(LocalizedResourceManager.GetString("UpdateCommandUnableToLocateNuGetExe"));
                }

                // Get the exe path and move it to a temp file (NuGet.exe.old) so we can replace the running exe with the bits we got 
                // from the package repository
                string renamedPath = exePath + ".old";
                Move(exePath, renamedPath);

                // Update the file
                UpdateFile(exePath, file);

                Console.WriteLine(LocalizedResourceManager.GetString("UpdateCommandUpdateSuccessful"));
            }
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We don't want this method to throw.")]
        internal static SemanticVersion GetNuGetVersion(ICustomAttributeProvider assembly)
        {
            try
            {
                var assemblyInformationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                return new SemanticVersion(assemblyInformationalVersion.InformationalVersion);
            }
            catch
            {
                // Don't let GetCustomAttributes throw.
            }
            return null;
        }

        protected virtual void UpdateFile(string exePath, IPackageFile file)
        {
            using (Stream fromStream = file.GetStream(), toStream = File.Create(exePath))
            {
                fromStream.CopyTo(toStream);
            }
        }

        protected virtual void Move(string oldPath, string newPath)
        {
            try
            {
                if (File.Exists(newPath))
                {
                    File.Delete(newPath);
                }
            }
            catch (FileNotFoundException)
            {

            }

            File.Move(oldPath, newPath);
        }
    }
}
