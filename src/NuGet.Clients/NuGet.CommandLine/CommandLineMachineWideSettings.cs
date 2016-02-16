using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using NuGet.Common;

namespace NuGet
{
    [Export(typeof(Configuration.IMachineWideSettings))]
    public class CommandLineMachineWideSettings : Configuration.IMachineWideSettings
    {
        Lazy<IEnumerable<Configuration.Settings>> _settings;

        public CommandLineMachineWideSettings()
        {
            var baseDirectory = NuGetEnvironment.GetFolderPath(NuGetFolderPath.MachineWideConfigDirectory);
            _settings = new Lazy<IEnumerable<Configuration.Settings>>(
                () => Configuration.Settings.LoadMachineWideSettings(baseDirectory));
        }

        public IEnumerable<Configuration.Settings> Settings
        {
            get
            {
                return _settings.Value;
            }
        }
    }
}
