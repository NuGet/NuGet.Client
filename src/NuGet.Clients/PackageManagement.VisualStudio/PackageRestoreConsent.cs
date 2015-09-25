// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using NuGet.Configuration;

namespace NuGet.PackageManagement.VisualStudio
{
    public class PackageRestoreConsent
    {
        private const string EnvironmentVariableName = "EnableNuGetPackageRestore";
        private const string PackageRestoreSection = "packageRestore";
        private const string PackageRestoreConsentKey = "enabled";

        // the key to enable/disable automatic package restore during build.
        private const string PackageRestoreAutomaticKey = "automatic";

        private readonly Configuration.ISettings _settings;
        private readonly Configuration.IEnvironmentVariableReader _environmentReader;
        private readonly ConfigurationDefaults _configurationDefaults;

        public PackageRestoreConsent(Configuration.ISettings settings)
            : this(settings, new EnvironmentVariableWrapper())
        {
        }

        public PackageRestoreConsent(Configuration.ISettings settings, Configuration.IEnvironmentVariableReader environmentReader)
            : this(settings, environmentReader, ConfigurationDefaults.Instance)
        {
        }

        public PackageRestoreConsent(Configuration.ISettings settings, Configuration.IEnvironmentVariableReader environmentReader, ConfigurationDefaults configurationDefaults)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            if (environmentReader == null)
            {
                throw new ArgumentNullException("environmentReader");
            }

            if (configurationDefaults == null)
            {
                throw new ArgumentNullException("configurationDefaults");
            }

            _settings = settings;
            _environmentReader = environmentReader;
            _configurationDefaults = configurationDefaults;
        }

        public bool IsGranted
        {
            get
            {
                string envValue = _environmentReader.GetEnvironmentVariable(EnvironmentVariableName);

                return IsGrantedInSettings || IsSet(envValue, false);
            }
        }

        public bool IsGrantedInSettings
        {
            get
            {
                string settingsValue = _settings.GetValue(PackageRestoreSection, PackageRestoreConsentKey);
                if (String.IsNullOrWhiteSpace(settingsValue))
                {
                    settingsValue = _configurationDefaults.DefaultPackageRestoreConsent ?? string.Empty;
                }

                return IsSet(settingsValue, true);
            }
            set { _settings.SetValue(PackageRestoreSection, PackageRestoreConsentKey, value.ToString(CultureInfo.InvariantCulture)); }
        }

        public bool IsAutomatic
        {
            get
            {
                string settingsValue = _settings.GetValue(PackageRestoreSection, PackageRestoreAutomaticKey);

                return IsSet(settingsValue, IsGrantedInSettings);
            }
            set { _settings.SetValue(PackageRestoreSection, PackageRestoreAutomaticKey, value.ToString(CultureInfo.InvariantCulture)); }
        }

        private static bool IsSet(string value, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            value = value.Trim();

            bool boolResult;
            int intResult;

            var result = ((Boolean.TryParse(value, out boolResult) && boolResult) ||
                          (Int32.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out intResult) && (intResult == 1)));

            return result;
        }
    }
}
