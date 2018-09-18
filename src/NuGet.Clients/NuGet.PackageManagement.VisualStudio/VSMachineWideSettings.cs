// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(Configuration.IMachineWideSettings))]
    public class VsMachineWideSettings : Configuration.IMachineWideSettings
    {
        private readonly AsyncLazy<Configuration.ISettings> _settings;
        private readonly IAsyncServiceProvider _asyncServiceProvider = AsyncServiceProvider.GlobalProvider;

        [ImportingConstructor]
        public VsMachineWideSettings(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            _settings = new AsyncLazy<Configuration.ISettings>(async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    var baseDirectory = Common.NuGetEnvironment.GetFolderPath(
                        Common.NuGetFolderPath.MachineWideConfigDirectory);

                    var dte = serviceProvider.GetDTE();

                    return Configuration.Settings.LoadMachineWideSettings(
                        baseDirectory,
                        "VisualStudio",
                        dte.Version,
                        dte.GetSKU());
                }, 
                ThreadHelper.JoinableTaskFactory);
        }

        public Configuration.ISettings Settings => NuGetUIThreadHelper.JoinableTaskFactory.Run(_settings.GetValueAsync);
    }
}
