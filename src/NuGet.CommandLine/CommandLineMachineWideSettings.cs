using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace NuGet
{
    [Export(typeof(Configuration.IMachineWideSettings))]
    public class CommandLineMachineWideSettings : Configuration.IMachineWideSettings
    {
        Lazy<IEnumerable<Configuration.Settings>> _settings;

        public CommandLineMachineWideSettings()
        {
            var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
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
