// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using XmlUtility = NuGet.Shared.XmlUtility;

namespace NuGet.CommandLine
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public abstract class DownloadCommandBase : Command
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        private readonly List<string> _sources = new List<string>();

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected PackageSaveMode EffectivePackageSaveMode { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected DownloadCommandBase()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
        }

        [Option(typeof(NuGetCommand), "CommandSourceDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public ICollection<string> Source
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            get { return _sources; }
        }

        [Option(typeof(NuGetCommand), "CommandFallbackSourceDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public ICollection<string> FallbackSource { get; } = new List<string>();
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "CommandNoCache", isHidden: true)]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool NoCache { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "CommandNoHttpCache")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool NoHttpCache { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "CommandDirectDownload")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool DirectDownload { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "CommandDisableParallelProcessing")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool DisableParallelProcessing { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "CommandPackageSaveMode")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string PackageSaveMode { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        /// <summary>
        /// If true the global packages folder NOT will be added as a source.
        /// </summary>
        internal bool ExcludeCacheAsSource { get; set; }

        internal void CalculateEffectivePackageSaveMode()
        {
            string packageSaveModeValue = PackageSaveMode;
            if (string.IsNullOrEmpty(packageSaveModeValue))
            {
                packageSaveModeValue = SettingsUtility.GetConfigValue(Settings, "PackageSaveMode");
            }

            if (!string.IsNullOrEmpty(packageSaveModeValue))
            {
                // The PackageSaveMode flag only determines if nuspec and nupkg are saved at the target location.
                // For install \ restore, we always extract files.
                EffectivePackageSaveMode = Packaging.PackageSaveMode.Files;
                foreach (var v in packageSaveModeValue.Split(';'))
                {
                    if (v.Equals(Packaging.PackageSaveMode.Nupkg.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        EffectivePackageSaveMode |= Packaging.PackageSaveMode.Nupkg;
                    }
                    else if (v.Equals(Packaging.PackageSaveMode.Nuspec.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        EffectivePackageSaveMode |= Packaging.PackageSaveMode.Nuspec;
                    }
                    else
                    {
                        string message = string.Format(
                            CultureInfo.CurrentCulture,
                            LocalizedResourceManager.GetString("Warning_InvalidPackageSaveMode"),
                            v);

                        throw new InvalidOperationException(message);
                    }
                }
            }
            else
            {
                EffectivePackageSaveMode = Packaging.PackageSaveMode.None;
            }
        }

        internal IEnumerable<PackageReference> GetInstalledPackageReferences(string projectConfigFilePath)
        {
            if (File.Exists(projectConfigFilePath))
            {
                try
                {
                    XDocument xDocument = XmlUtility.Load(projectConfigFilePath);
                    var reader = new PackagesConfigReader(xDocument);
                    return reader.GetPackages(allowDuplicatePackageIds: true);
                }
                catch (XmlException ex)
                {
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString("Error_PackagesConfigParseError"),
                        projectConfigFilePath,
                        ex.Message);

                    throw new CommandException(message);
                }
            }

            return Enumerable.Empty<Packaging.PackageReference>();
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        protected IReadOnlyCollection<Configuration.PackageSource> GetPackageSources(Configuration.ISettings settings)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            var availableSources = SourceProvider.LoadPackageSources().Where(source => source.IsEnabled);
            var packageSources = new List<Configuration.PackageSource>();

            if (!(NoCache || NoHttpCache) && !ExcludeCacheAsSource)
            {
                // Add the v3 global packages folder
                var globalPackageFolder = SettingsUtility.GetGlobalPackagesFolder(settings);

                if (!string.IsNullOrEmpty(globalPackageFolder) && Directory.Exists(globalPackageFolder))
                {
                    packageSources.Add(new FeedTypePackageSource(globalPackageFolder, FeedType.FileSystemV3));
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
