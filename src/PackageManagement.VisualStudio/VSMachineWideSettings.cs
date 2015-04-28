using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(IMachineWideSettings))]
    public class VsMachineWideSettings : IMachineWideSettings
    {
        AsyncLazy<IEnumerable<Settings>> _settings;

        [ImportingConstructor]
        public VsMachineWideSettings()
            : this(ServiceLocator.GetInstance<DTE>())
        {
        }

        internal VsMachineWideSettings(DTE dte)
        {
            var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _settings = new AsyncLazy<IEnumerable<Settings>>(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return NuGet.Configuration.Settings.LoadMachineWideSettings(
                      baseDirectory,
                      "VisualStudio",
                      dte.Version,
                      VSVersionHelper.GetSKU());
                }, ThreadHelper.JoinableTaskFactory);
        }

        public IEnumerable<Settings> Settings
        {
            get
            {
                return ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    return await _settings.GetValueAsync();
                });
            }
        }
    }
}
