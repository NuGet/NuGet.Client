// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;

namespace NuGetConsole
{
    [Export(typeof(IOutputConsoleProvider))]
    public class OutputConsoleProvider : IOutputConsoleProvider
    {
        private readonly IEnumerable<Lazy<IHostProvider, IHostMetadata>> _hostProviders;
        private readonly AsyncLazy<IVsOutputWindow> _vsOutputWindow;
        private readonly AsyncLazy<IVsUIShell> _vsUIShell;
        private readonly Lazy<IConsole> _cachedOutputConsole;

        private IVsOutputWindow VsOutputWindow => NuGetUIThreadHelper.JoinableTaskFactory.Run(_vsOutputWindow.GetValueAsync);

        private IVsUIShell VsUIShell => NuGetUIThreadHelper.JoinableTaskFactory.Run(_vsUIShell.GetValueAsync);

        [ImportingConstructor]
        OutputConsoleProvider(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider,
            [ImportMany]
            IEnumerable<Lazy<IHostProvider, IHostMetadata>> hostProviders)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (hostProviders == null)
            {
                throw new ArgumentNullException(nameof(hostProviders));
            }

            _hostProviders = hostProviders;

            _vsOutputWindow = new AsyncLazy<IVsOutputWindow>(
                async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return serviceProvider.GetService<SVsOutputWindow, IVsOutputWindow>();
                },
                NuGetUIThreadHelper.JoinableTaskFactory);

            _vsUIShell = new AsyncLazy<IVsUIShell>(
                async () =>
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return serviceProvider.GetService<SVsUIShell, IVsUIShell>();
                },
                NuGetUIThreadHelper.JoinableTaskFactory);

            _cachedOutputConsole = new Lazy<IConsole>(
                () => new OutputConsole(VsOutputWindow, VsUIShell));
        }

        public IOutputConsole CreateBuildOutputConsole()
        {
            return new BuildOutputConsole(VsOutputWindow);
        }

        public IOutputConsole CreatePackageManagerConsole()
        {
            return _cachedOutputConsole.Value;
        }

        public IConsole CreatePowerShellConsole()
        {
            return CreateOutputConsole(requirePowerShellHost: true);
        }

        public IConsole CreateOutputConsole(bool requirePowerShellHost)
        {
            var console = _cachedOutputConsole.Value;

            // only instantiate the PS host if necessary (e.g. when package contains PS script files)
            if (requirePowerShellHost && console.Host == null)
            {
                var hostProvider = GetPowerShellHostProvider();
                console.Host = hostProvider.CreateHost(@async: false);
            }

            return console;
        }

        private IHostProvider GetPowerShellHostProvider()
        {
            // The PowerConsole design enables multiple hosts (PowerShell, Python, Ruby)
            // For the Output window console, we're only interested in the PowerShell host. 
            // Here we filter out the PowerShell host provider based on its name.

            // The PowerShell host provider name is defined in PowerShellHostProvider.cs
            const string PowerShellHostProviderName = "NuGetConsole.Host.PowerShell";

            var psProvider = _hostProviders
                .Single(export => export.Metadata.HostName == PowerShellHostProviderName);

            return psProvider.Value;
        }
    }
}
