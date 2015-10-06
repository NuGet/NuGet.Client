// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Configuration;

namespace NuGet.Credentials
{
    /// <summary>
    /// Discovers plugin providers.
    /// </summary>
    public class PluginCredentialProviderBuilder
    {
        private readonly Configuration.ISettings _settings;
        private readonly Configuration.IEnvironmentVariableReader _envarReader;
        private readonly IExtensionLocator _extensionLocator;

        public PluginCredentialProviderBuilder(IExtensionLocator extensionLocator, Configuration.ISettings settings) 
            : this(extensionLocator, settings, new EnvironmentVariableWrapper())
        {
        }

        public PluginCredentialProviderBuilder(
            IExtensionLocator extensionLocator,
            Configuration.ISettings settings,
            Configuration.IEnvironmentVariableReader envarReader)
        {
            if (extensionLocator == null)
            {
                throw new ArgumentNullException(nameof(extensionLocator));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (envarReader == null)
            {
                throw new ArgumentNullException(nameof(envarReader));
            }

            _extensionLocator = extensionLocator;
            _settings = settings;
            _envarReader = envarReader;

        }

        /// <summary>
        /// Plugin providers are entered loaded the same way as other nuget extensions,
        /// matching any extension named CredentialProvider.*.exe.
        /// </summary>
        /// <returns>An enumeration of plugin providers</returns>
        public IEnumerable<ICredentialProvider> BuildAll()
        {
            var timeout = TimeoutSeconds;
            var pluginPaths = _extensionLocator.FindCredentialProviders();

            // Sort the plugin providers by filename within each directory
            // so that we load them in a predictable order
            // but still respect the precedence of directories loaded by
            // ExtensionLocator
            var plugins = pluginPaths
                .GroupBy(Path.GetDirectoryName)
                .SelectMany(g => g.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                .Select(x => new PluginCredentialProvider(x, timeout));

            return plugins;
        }

        private int TimeoutSeconds
        {
            get
            {
                var timeoutSetting = _settings.GetValue(
                    SettingsUtility.ConfigSection,
                    CredentialsConstants.ProviderTimeoutSecondsSetting);

                var timeoutEnvar = _envarReader.GetEnvironmentVariable(
                    CredentialsConstants.ProviderTimeoutSecondsEnvar);

                int value;
                if (int.TryParse(timeoutSetting, out value)
                    || int.TryParse(timeoutEnvar, out value))
                {
                    return value;
                }

                return CredentialsConstants.ProviderTimeoutSecondsDefault;
            }
        }
    }
}
