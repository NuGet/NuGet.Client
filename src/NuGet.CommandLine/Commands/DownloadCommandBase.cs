using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Configuration;
using NuGet.Packaging;

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

        protected Configuration.ISettings ReadSettings(string workingDirectory)
        {
            Configuration.ISettings settings;
            if (!string.IsNullOrEmpty(ConfigFile))
            {
                settings = Configuration.Settings.LoadDefaultSettings(Path.GetFullPath(ConfigFile),
                    configFileName: null,
                    machineWideSettings: null);
            }
            else
            {
                settings = Configuration.Settings.LoadDefaultSettings(workingDirectory,
                    configFileName: null,
                    machineWideSettings: null);
            }

            return settings;
        }

        protected IEnumerable<Packaging.PackageReference> GetInstalledPackageReferences(string projectConfigFilePath)
        {
            if (File.Exists(projectConfigFilePath))
            {
                var reader = new PackagesConfigReader(XDocument.Load(projectConfigFilePath));
                return reader.GetPackages();
            }

            return Enumerable.Empty<Packaging.PackageReference>();
        }

        protected IEnumerable<Configuration.PackageSource> GetPackageSources(Configuration.ISettings settings)
        {
            var packageSourceProvider = new Configuration.PackageSourceProvider(settings);
            var availableSources = packageSourceProvider.LoadPackageSources().Where(source => source.IsEnabled);
            var packageSources = new List<Configuration.PackageSource>();
            foreach (var source in Source)
            {
                packageSources.Add(Common.PackageSourceProviderExtensions.ResolveSource(packageSources, source));
            }

            if (packageSources.Count == 0)
            {
                packageSources.AddRange(packageSourceProvider.LoadPackageSources());
            }

            foreach (var source in FallbackSource)
            {
                packageSources.Add(Common.PackageSourceProviderExtensions.ResolveSource(packageSources, source));
            }

            return packageSources;
        }
    }
}