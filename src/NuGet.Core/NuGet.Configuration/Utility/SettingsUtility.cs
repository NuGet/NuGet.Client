// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Common;
using System.Globalization;

namespace NuGet.Configuration
{
    public static class SettingsUtility
    {
        public static readonly string ConfigSection = "config";
        private const string GlobalPackagesFolderKey = "globalPackagesFolder";
        private const string GlobalPackagesFolderEnvironmentKey = "NUGET_PACKAGES";
        private const string FallbackPackagesFolderEnvironmentKey = "NUGET_FALLBACK_PACKAGES";
        private const string HttpCacheEnvironmentKey = "NUGET_HTTP_CACHE_PATH";
        private const string RepositoryPathKey = "repositoryPath";
        public static readonly string DefaultGlobalPackagesFolderPath = "packages" + Path.DirectorySeparatorChar;

        public static string GetRepositoryPath(ISettings settings)
        {
            var path = settings.GetValue(ConfigSection, RepositoryPathKey, isPath: true);
            if (!String.IsNullOrEmpty(path))
            {
                path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return path;
        }

        public static string GetDecryptedValue(ISettings settings, string section, string key, bool isPath = false)
        {
            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "section");
            }

            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "key");
            }

            var encryptedString = settings.GetValue(section, key, isPath);
            if (encryptedString == null)
            {
                return null;
            }

            if (String.IsNullOrEmpty(encryptedString))
            {
                return String.Empty;
            }
            return EncryptionUtility.DecryptString(encryptedString);
        }

        public static void SetEncryptedValue(ISettings settings, string section, string key, string value)
        {
            if (String.IsNullOrEmpty(section))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "section");
            }

            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, "key");
            }

            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            if (String.IsNullOrEmpty(value))
            {
                settings.SetValue(section, key, String.Empty);
            }
            else
            {
                var encryptedString = EncryptionUtility.EncryptString(value);
                settings.SetValue(section, key, encryptedString);
            }
        }

        /// <summary>
        /// Retrieves a config value for the specified key
        /// </summary>
        /// <param name="settings">The settings instance to retrieve </param>
        /// <param name="key">The key to look up</param>
        /// <param name="decrypt">Determines if the retrieved value needs to be decrypted.</param>
        /// <param name="isPath">Determines if the retrieved value is returned as a path.</param>
        /// <returns>Null if the key was not found, value from config otherwise.</returns>
        public static string GetConfigValue(ISettings settings, string key, bool decrypt = false, bool isPath = false)
        {
            return decrypt ?
                GetDecryptedValue(settings, ConfigSection, key, isPath) :
                settings.GetValue(ConfigSection, key, isPath);
        }

        /// <summary>
        /// Sets a config value in the setting.
        /// </summary>
        /// <param name="settings">The settings instance to store the key-value in.</param>
        /// <param name="key">The key to store.</param>
        /// <param name="value">The value to store.</param>
        /// <param name="encrypt">Determines if the value needs to be encrypted prior to storing.</param>
        public static void SetConfigValue(ISettings settings, string key, string value, bool encrypt = false)
        {
            if (encrypt == true)
            {
                SetEncryptedValue(settings, ConfigSection, key, value);
            }
            else
            {
                settings.SetValue(ConfigSection, key, value);
            }
        }

        /// <summary>
        /// Deletes a config value from settings
        /// </summary>
        /// <param name="settings">The settings instance to delete the key from.</param>
        /// <param name="key">The key to delete.</param>
        /// <returns>True if the value was deleted, false otherwise.</returns>
        public static bool DeleteConfigValue(ISettings settings, string key)
        {
            return settings.DeleteValue(ConfigSection, key);
        }

        public static string GetGlobalPackagesFolder(ISettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var path = Environment.GetEnvironmentVariable(GlobalPackagesFolderEnvironmentKey);
            if (string.IsNullOrEmpty(path))
            {
                // Environment variable for globalPackagesFolder is not set.
                path = settings.GetValue(ConfigSection, GlobalPackagesFolderKey, isPath: true);
            }
            else
            {
                // Verify the path is absolute
                VerifyPathIsRooted(GlobalPackagesFolderEnvironmentKey, path);
            }

            if (!string.IsNullOrEmpty(path))
            {
                path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                path = Path.GetFullPath(path);
                return path;
            }

            path = Path.Combine(NuGetEnvironment.GetFolderPath(NuGetFolderPath.NuGetHome), DefaultGlobalPackagesFolderPath);

            return path;
        }

        /// <summary>
        /// Read fallback folders from the environment variable or from nuget.config.
        /// </summary>
        public static IReadOnlyList<string> GetFallbackPackageFolders(ISettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var paths = new List<string>();

            var envValue = Environment.GetEnvironmentVariable(FallbackPackagesFolderEnvironmentKey);

            if (string.IsNullOrEmpty(envValue))
            {
                // read config values
                paths.AddRange(GetFallbackPackageFoldersFromConfig(settings));
            }
            else
            {
                paths.AddRange(envValue.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

                // Verify the path is absolute
                foreach (var path in paths)
                {
                    VerifyPathIsRooted(FallbackPackagesFolderEnvironmentKey, path);
                }
            }

            for (int i=0; i < paths.Count; i++)
            {
                paths[i] = paths[i].Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                paths[i] = Path.GetFullPath(paths[i]);
            }

            return paths;
        }

        /// <summary>
        /// Read fallback folders only from nuget.config.
        /// </summary>
        private static IReadOnlyList<string> GetFallbackPackageFoldersFromConfig(ISettings settings)
        {
            var fallbackValues = settings.GetSettingValues(ConfigurationConstants.FallbackPackageFolders, isPath: true) ??
                                      Enumerable.Empty<SettingValue>();

            return fallbackValues
                .OrderByDescending(setting => setting.Priority)
                .Select(setting => setting.Value)
                .ToList();
        }

        /// <summary>
        /// Get the HTTP cache folder from either an environment variable or a default.
        /// </summary>
        public static string GetHttpCacheFolder()
        {
            var path = Environment.GetEnvironmentVariable(HttpCacheEnvironmentKey);
            if (!string.IsNullOrEmpty(path))
            {
                // Verify the path is absolute
                VerifyPathIsRooted(HttpCacheEnvironmentKey, path);
            }

            if (!string.IsNullOrEmpty(path))
            {
                path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                path = Path.GetFullPath(path);
                return path;
            }
            
            return NuGetEnvironment.GetFolderPath(NuGetFolderPath.HttpCacheDirectory);
        }

        public static IEnumerable<PackageSource> GetEnabledSources(ISettings settings)
        {
            var provider = new PackageSourceProvider(settings);
            return provider.LoadPackageSources().Where(e => e.IsEnabled == true).ToList();
        }

        /// <summary>
        /// The DefaultPushSource can be:
        /// - An absolute URL
        /// - An absolute file path
        /// - A relative file path
        /// - The name of a registered source from a config file
        /// </summary>
        public static string GetDefaultPushSource(ISettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            string source = settings.GetValue(ConfigurationConstants.Config, ConfigurationConstants.DefaultPushSource, isPath: false);

            Uri sourceUri = UriUtility.TryCreateSourceUri(source, UriKind.RelativeOrAbsolute);
            if (sourceUri != null && !sourceUri.IsAbsoluteUri)
            {
                // For non-absolute sources, it could be the name of a config source, or a relative file path.
                IPackageSourceProvider sourceProvider = new PackageSourceProvider(settings);
                IEnumerable<PackageSource> allSources = sourceProvider.LoadPackageSources();

                if (!allSources.Any(s => s.IsEnabled && s.Name.Equals(source, StringComparison.OrdinalIgnoreCase)))
                {
                    // It wasn't the name of a source, so treat it like a relative file path
                    source = settings.GetValue(ConfigurationConstants.Config, ConfigurationConstants.DefaultPushSource, isPath: true);
                }
            }

            return source;
        }

        public static IEnumerable<string> GetConfigFilePaths(ISettings settings)
        {
            if (!(settings is NullSettings))
            {
                return settings.Priority.Select(config => Path.GetFullPath(Path.Combine(config.Root, config.FileName)));
            }
            else
            {
                return new List<string>();
            }
        }

        private static string GetPathFromEnvOrConfig(string envVarName, string configKey, ISettings settings)
        {
            var path = Environment.GetEnvironmentVariable(envVarName);

            if (!string.IsNullOrEmpty(path))
            {
                if (!Path.IsPathRooted(path))
                {
                    var message = String.Format(CultureInfo.CurrentCulture, Resources.RelativeEnvVarPath, envVarName, path);
                    throw new NuGetConfigurationException(message);
                }
            }
            else
            {
                path = Path.Combine(NuGetEnvironment.GetFolderPath(NuGetFolderPath.NuGetHome), DefaultGlobalPackagesFolderPath);
            }

            if (!string.IsNullOrEmpty(path))
            {
                path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return path;
        }

        /// <summary>
        /// Throw if a path is relative.
        /// </summary>
        private static void VerifyPathIsRooted(string key, string path)
        {
            if (!Path.IsPathRooted(path))
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.MustContainAbsolutePath,
                    key,
                    path);

                throw new NuGetConfigurationException(message);
            }
        }
    }
}
