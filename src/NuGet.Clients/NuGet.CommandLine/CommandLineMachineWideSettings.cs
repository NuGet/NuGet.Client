using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using NuGet.Common;

namespace NuGet.CommandLine
{
    [Export(typeof(Configuration.IMachineWideSettings))]
    public class CommandLineMachineWideSettings : Configuration.IMachineWideSettings
    {
        Lazy<Configuration.Settings> _settings;

        public CommandLineMachineWideSettings()
        {
            var baseDirectory = NuGetEnvironment.GetFolderPath(NuGetFolderPath.MachineWideConfigDirectory);
            _settings = new Lazy<Configuration.Settings>(
                () => Configuration.Settings.LoadMachineWideSettings(baseDirectory));
        }

        public Configuration.Settings Settings => _settings.Value;
    }
}
