using EnvDTE;
using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(IMachineWideSettings))]
    public class VsMachineWideSettings : IMachineWideSettings
    {
        Lazy<IEnumerable<Settings>> _settings;

        [ImportingConstructor]
        public VsMachineWideSettings()
            : this(ServiceLocator.GetInstance<DTE>())
        {
        }

        internal VsMachineWideSettings(DTE dte)
        {
            var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _settings = new Lazy<IEnumerable<Settings>>(
                () =>
                {

                    return NuGet.Configuration.Settings.LoadMachineWideSettings(
                        baseDirectory,
                        "VisualStudio",
                        dte.Version,
                        VSVersionHelper.GetSKU());
                });
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
