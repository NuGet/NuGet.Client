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
    /// Discovers configured plugin providers.
    /// </summary>
    public class PluginCredentialProviderBuilder
    {
        private readonly Configuration.ISettings _settings;
        private readonly Configuration.IEnvironmentVariableReader _envarReader;

        public PluginCredentialProviderBuilder(Configuration.ISettings settings) : this(settings, new EnvironmentVariableWrapper())
        {
        }

        public PluginCredentialProviderBuilder(Configuration.ISettings settings, Configuration.IEnvironmentVariableReader envarReader)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            if (envarReader == null)
            {
                throw new ArgumentNullException(nameof(envarReader));
            }

            _settings = settings;
            _envarReader = envarReader;

        }

        /// <summary>
        /// Find all configured plugin providers.
        /// Plugin providers are entered in settings files with a key prefixed
        /// "CredentialProvider.Plugin." and with a value that is an absolute or
        /// relative path to a plugin application.  Relative paths will be searched from
        /// the following roots:  
        /// 1. Current executing assembly directory
        /// 2. %NUGET_EXTENSIONS_PATH%
        /// </summary>
        /// <returns>An enumeration of plugin providers</returns>
        public IEnumerable<ICredentialProvider> BuildAll()
        {
            var plugins = new List<ICredentialProvider>();
            var resolvedPaths = GetResolvedPaths().ToList();
            var timeout = TimeoutSeconds;

            foreach (var path in resolvedPaths)
            {
                if(path.resolved == null)
                {
                    string configuredPath = path.path;
                    string attemptedPaths = string.Join(", ", Probe(path.path));
                    throw PluginException.CreatePathNotFoundMessage(configuredPath, attemptedPaths);
                }

                plugins.Add(new PluginCredentialProvider(path.resolved, timeout));
            }

            return plugins;
        }

        internal IEnumerable<dynamic> GetResolvedPaths()
        {
            return _settings.GetSettingValues(CredentialsConstants.SettingsConfigSection)
                .Where(x => x.Key.StartsWith(CredentialsConstants.PluginPrefixSetting))
                .Select(x => x.Value)
                .Select(x => new { path = x, resolved = Locate(x) });
        }

        private string Locate(string path)
        {
            return Probe(path).FirstOrDefault(File.Exists);
        }

        private IEnumerable<string> Probe(string path)
        {
            if (Path.IsPathRooted(path))
            {
                yield return path;
            }
            else
            {
                yield return Path.Combine(
                    Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    path);
            }

            var extensionsPathEnvar = _envarReader.GetEnvironmentVariable(
                CredentialsConstants.ExtensionsPathEnvar);
            if (extensionsPathEnvar != null)
            {
                yield return Path.Combine(extensionsPathEnvar,path);
            }
        }

        private int TimeoutSeconds
        {
            get
            {
                int value;
                var timeoutSetting = _settings.GetValue(
                    CredentialsConstants.SettingsConfigSection,
                    CredentialsConstants.ProviderTimeoutSecondsSetting);
                var timeoutEnvar = _envarReader.GetEnvironmentVariable(
                    CredentialsConstants.ProviderTimeoutSecondsEnvar);

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
