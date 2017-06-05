// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Configuration;
using NuGet.Configuration;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class PluginConfiguration
    {
        internal IEnumerable<PluginPackageSource> PluginPackageSources { get; }

        private PluginConfiguration(IEnumerable<PluginPackageSource> pluginPackageSources)
        {
            Assert.IsNotNull(pluginPackageSources, nameof(pluginPackageSources));

            PluginPackageSources = pluginPackageSources;
        }

        internal static PluginConfiguration Create()
        {
            var pluginPackageSources = new List<PluginPackageSource>();
            var settings = ConfigurationManager.AppSettings;

            if (settings != null)
            {
                foreach (var key in settings.AllKeys)
                {
                    var packageSource = new PackageSource(key);
                    var exposeNupkgFilesToNuGet = bool.Parse(settings[key]);
                    var pluginPackageSource = new PluginPackageSource(packageSource, exposeNupkgFilesToNuGet);

                    pluginPackageSources.Add(pluginPackageSource);
                }
            }

            return new PluginConfiguration(pluginPackageSources);
        }
    }
}