using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol.Core.v2;

namespace NuGet.CommandLine
{
    public abstract class DownloadCommandBase : Command
    {
        private readonly IPackageRepository _cacheRepository;
        private readonly List<string> _sources = new List<string>();

        protected PackageSaveModes EffectivePackageSaveMode { get; set; }

        protected DownloadCommandBase(IPackageRepository cacheRepository)
        {
            _cacheRepository = cacheRepository;
        }

        [Option(typeof(NuGetCommand), "CommandSourceDescription")]
        public ICollection<string> Source
        {
            get { return _sources; }
        }

        [Option(typeof(NuGetCommand), "CommandFallbackSourceDescription")]
        public ICollection<string> FallbackSource { get; } = new List<string>();

        [Option(typeof(NuGetCommand), "CommandNoCache")]
        public bool NoCache { get; set; }

        [Option(typeof(NuGetCommand), "CommandDisableParallelProcessing")]
        public bool DisableParallelProcessing { get; set; }

        [Option(typeof(NuGetCommand), "CommandPackageSaveMode")]
        public string PackageSaveMode { get; set; }

        internal void CalculateEffectivePackageSaveMode()
        {
            string packageSaveModeValue = PackageSaveMode;
            if (string.IsNullOrEmpty(packageSaveModeValue))
            {
                packageSaveModeValue = SettingsUtility.GetConfigValue(Settings, "PackageSaveMode");
            }

            EffectivePackageSaveMode = PackageSaveModes.None;
            if (!string.IsNullOrEmpty(packageSaveModeValue))
            {
                foreach (var v in packageSaveModeValue.Split(';'))
                {
                    if (v.Equals(PackageSaveModes.Nupkg.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        EffectivePackageSaveMode |= PackageSaveModes.Nupkg;
                    }
                    else if (v.Equals(PackageSaveModes.Nuspec.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        EffectivePackageSaveMode |= PackageSaveModes.Nuspec;
                    }
                    else
                    {
                        string message = String.Format(
                            CultureInfo.CurrentCulture,
                            LocalizedResourceManager.GetString("Warning_InvalidPackageSaveMode"),
                            v);

                        throw new InvalidOperationException(message);
                    }
                }
            }
        }

        protected IEnumerable<Packaging.PackageReference> GetInstalledPackageReferences(
            string projectConfigFilePath,
            bool allowDuplicatePackageIds)
        {
            if (File.Exists(projectConfigFilePath))
            {
                try
                {
                    var xDocument = XDocument.Load(projectConfigFilePath);
                    var reader = new PackagesConfigReader(XDocument.Load(projectConfigFilePath));
                    return reader.GetPackages(allowDuplicatePackageIds);
                }
                catch (XmlException ex)
                {
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString("Error_PackagesConfigParseError"),
                        projectConfigFilePath,
                        ex.Message);

                    throw new CommandLineException(message);
                }
            }

            return Enumerable.Empty<Packaging.PackageReference>();
        }

        protected IReadOnlyCollection<Configuration.PackageSource> GetPackageSources(Configuration.ISettings settings)
        {
            var availableSources = SourceProvider.LoadPackageSources().Where(source => source.IsEnabled);
            var packageSources = new List<Configuration.PackageSource>();

            if (!NoCache)
            {
                // Add the v2 machine cache
                if (!string.IsNullOrEmpty(MachineCache.Default?.Source))
                {
                    packageSources.Add(new V2PackageSource(MachineCache.Default.Source, () => MachineCache.Default));
                }

                // Add the v3 global packages folder
                var globalPackageFolder = SettingsUtility.GetGlobalPackagesFolder(settings);

                if (!string.IsNullOrEmpty(globalPackageFolder) && Directory.Exists(globalPackageFolder))
                {
                    packageSources.Add(new V2PackageSource(globalPackageFolder,
                        () => new LocalPackageRepository(globalPackageFolder)));
                }
            }

            foreach (var source in Source)
            {
                packageSources.Add(Common.PackageSourceProviderExtensions.ResolveSource(availableSources, source));
            }

            if (Source.Count == 0)
            {
                packageSources.AddRange(availableSources);
            }

            foreach (var source in FallbackSource)
            {
                packageSources.Add(Common.PackageSourceProviderExtensions.ResolveSource(packageSources, source));
            }

            return packageSources;
        }
    }
}