// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;

namespace StandaloneUI
{
    internal class StandaloneUIContext : NuGetUIContextBase
    {
        private readonly string _settingsFile;
        private Dictionary<string, UserSettings> _settings;

        public StandaloneUIContext(
            string settingsFile,
            ISourceRepositoryProvider sourceProvider,
            ISolutionManager solutionManager,
            NuGetPackageManager packageManager,
            UIActionEngine uiActionEngine,
            IPackageRestoreManager packageRestoreManager,
            IOptionsPageActivator optionsPageActivator,
            IEnumerable<NuGetProject> projects)
            :
                base(sourceProvider,
                    solutionManager,
                    packageManager,
                    uiActionEngine,
                    packageRestoreManager,
                    optionsPageActivator,
                    projects,
                    packageManagerProviders: Enumerable.Empty<IVsPackageManagerProvider>())
        {
            _settingsFile = settingsFile;
            LoadSettings();
        }

        private void LoadSettings()
        {
            _settings = new Dictionary<string, UserSettings>();

            if (!File.Exists(_settingsFile))
            {
                return;
            }

            try
            {
                using (var reader = new StreamReader(_settingsFile))
                {
                    var str = reader.ReadToEnd();
                    var obj = JsonConvert.DeserializeObject<Dictionary<string, UserSettings>>(str);
                    if (obj != null)
                    {
                        _settings = obj;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public override void AddSettings(string key, UserSettings obj)
        {
            _settings[key] = obj;
        }

        public override UserSettings GetSettings(string key)
        {
            UserSettings settings;
            if (_settings.TryGetValue(key, out settings))
            {
                return settings;
            }
            return new UserSettings();
        }

        public override void PersistSettings()
        {
            try
            {
                var str = JsonConvert.SerializeObject(_settings);
                using (var writer = new StreamWriter(_settingsFile))
                {
                    writer.Write(str);
                }
            }
            catch (Exception)
            {
            }
        }

        public override void ApplyShowPreviewSetting(bool show)
        {
            // no-op
        }
    }
}
