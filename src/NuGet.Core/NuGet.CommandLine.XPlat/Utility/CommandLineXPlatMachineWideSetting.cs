using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.CommandLine.XPlat
{
    public class CommandLineXPlatMachineWideSetting : IMachineWideSettings
    {
        Lazy<IEnumerable<Settings>> _settings;

        public CommandLineXPlatMachineWideSetting()
        {
            var baseDirectory = NuGetEnvironment.GetFolderPath(NuGetFolderPath.MachineWideConfigDirectory);
            _settings = new Lazy<IEnumerable<Settings>>(
                () => Configuration.Settings.LoadMachineWideSettings(baseDirectory));
        }

        public IEnumerable<Settings> Settings
        {
            get
            {
                return _settings.Value;
            }
        }
    }
}
