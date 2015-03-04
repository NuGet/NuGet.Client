using System;
using System.Collections.Generic;
using System.IO;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.Protocol.Core.Types;

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
            IEnumerable<NuGet.ProjectManagement.NuGetProject> projects) :
            base(sourceProvider, solutionManager, packageManager, uiActionEngine, packageRestoreManager, optionsPageActivator, projects)
        {
            _settingsFile = settingsFile;            
            LoadSettings();
        }

        void LoadSettings()
        {
            _settings = new Dictionary<string, UserSettings>();
            try            
            {
                using (var reader = new StreamReader(_settingsFile))
                {
                    var str = reader.ReadToEnd();
                    var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, UserSettings>>(str);
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
            else
            {
                return null;
            }
        }

        public override void PersistSettings()
        {
            try
            {
                var str = Newtonsoft.Json.JsonConvert.SerializeObject(_settings);
                using (var writer = new StreamWriter(_settingsFile))
                {
                    writer.Write(str);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}