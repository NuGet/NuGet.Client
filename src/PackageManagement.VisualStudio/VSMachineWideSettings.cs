// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
        private readonly AsyncLazy<IEnumerable<Settings>> _settings;

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
                    return Configuration.Settings.LoadMachineWideSettings(
                        baseDirectory,
                        "VisualStudio",
                        dte.Version,
                        VSVersionHelper.GetSKU());
                }, ThreadHelper.JoinableTaskFactory);
        }

        public IEnumerable<Settings> Settings => ThreadHelper.JoinableTaskFactory.Run(_settings.GetValueAsync);
    }
}
