// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

extern alias CoreV2;

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using NuGet.Configuration;

using static CoreV2.NuGet.PackageRepositoryExtensions;
using static CoreV2.NuGet.CustomAttributeProviderExtensions;

//TODO: This can be reworked with V3 APIs - tracked in https://github.com/NuGet/Home/issues/4197
namespace NuGet.CommandLine
{
    /// <summary>
    /// Handles updating the executing instance of NuGet.exe
    /// </summary>
    public class SelfUpdater
    {
        private const string NuGetCommandLinePackageId = "NuGet.CommandLine";
        private const string NuGetExe = "NuGet.exe";

        private string _assemblyLocation;
        private readonly Lazy<string> _lazyAssemblyLocation = new Lazy<string>(() =>
        {
            var assembly = typeof(SelfUpdater).Assembly;
            return assembly.Location;
        });
        private readonly CoreV2.NuGet.IPackageRepositoryFactory _repositoryFactory;

        public SelfUpdater(CoreV2.NuGet.IPackageRepositoryFactory repositoryFactory)
        {
            if (repositoryFactory == null)
            {
                throw new ArgumentNullException("repositoryFactory");
            }
            _repositoryFactory = repositoryFactory;
        }

        public IConsole Console { get; set; }

        /// <summary>
        /// This property is used only for testing (so that the self updater does not replace the running test
        /// assembly).
        /// </summary>
        public string AssemblyLocation
        {
            get
            {
                return _assemblyLocation ?? _lazyAssemblyLocation.Value;
            }
            set
            {
                _assemblyLocation = value;
            }
        }

        public void UpdateSelf(bool prerelease)
        {
            Assembly assembly = typeof(SelfUpdater).Assembly;
            CoreV2.NuGet.SemanticVersion version = GetNuGetVersion(assembly) ?? new CoreV2.NuGet.SemanticVersion(assembly.GetName().Version);
            SelfUpdate(AssemblyLocation, prerelease, version);
        }

        internal void SelfUpdate(string exePath, bool prerelease, CoreV2.NuGet.SemanticVersion version)
        {
            Console.WriteLine(LocalizedResourceManager.GetString("UpdateCommandCheckingForUpdates"), NuGetConstants.V2FeedUrl);

            // Get the nuget command line package from the specified repository
            CoreV2.NuGet.IPackageRepository packageRepository = _repositoryFactory.CreateRepository(NuGetConstants.V2FeedUrl);
            CoreV2.NuGet.IPackage package = packageRepository.GetUpdates(
                new [] { new CoreV2.NuGet.PackageName(NuGetCommandLinePackageId, version) },
                includePrerelease: prerelease, 
                includeAllVersions: false, 
                targetFrameworks: null,
                versionConstraints: null).FirstOrDefault();
 
            Console.WriteLine(LocalizedResourceManager.GetString("UpdateCommandCurrentlyRunningNuGetExe"), version); // SemanticVersion is the problem

            // Check to see if an update is needed
            if (package == null || version >= package.Version)
            {
                Console.WriteLine(LocalizedResourceManager.GetString("UpdateCommandNuGetUpToDate"));
            }
            else
            {
                Console.WriteLine(LocalizedResourceManager.GetString("UpdateCommandUpdatingNuGet"), package.Version);

                // Get NuGet.exe file from the package
                CoreV2.NuGet.IPackageFile file = package.GetFiles().FirstOrDefault(f => Path.GetFileName(f.Path).Equals(NuGetExe, StringComparison.OrdinalIgnoreCase));

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
        internal static CoreV2.NuGet.SemanticVersion GetNuGetVersion(ICustomAttributeProvider assembly)
        {
            try
            {
                var assemblyInformationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                return new CoreV2.NuGet.SemanticVersion(assemblyInformationalVersion.InformationalVersion);
            }
            catch
            {
                // Don't let GetCustomAttributes throw.
            }
            return null;
        }

        protected virtual void UpdateFile(string exePath, CoreV2.NuGet.IPackageFile file)
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
