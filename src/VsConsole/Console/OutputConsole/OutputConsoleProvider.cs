// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement.VisualStudio;

namespace NuGetConsole
{
    [Export(typeof(IOutputConsoleProvider))]
    public class OutputConsoleProvider : IOutputConsoleProvider
    {
        private IConsole _console;

        public IConsole CreateOutputConsole(bool requirePowerShellHost)
        {
            if (_console == null)
            {
                var serviceProvider = ServiceLocator.GetInstance<IServiceProvider>();
                var outputWindow = (IVsOutputWindow)serviceProvider.GetService(typeof(SVsOutputWindow));
                Debug.Assert(outputWindow != null);

                _console = new OutputConsole(outputWindow);
            }

            // only instantiate the PS host if necessary (e.g. when package contains PS script files)
            if (requirePowerShellHost && _console.Host == null)
            {
                var hostProvider = GetPowerShellHostProvider();
                _console.Host = hostProvider.CreateHost(@async: false);
            }

            return _console;
        }

        private static IHostProvider GetPowerShellHostProvider()
        {
            // The PowerConsole design enables multiple hosts (PowerShell, Python, Ruby)
            // For the Output window console, we're only interested in the PowerShell host. 
            // Here we filter out the PowerShell host provider based on its name.

            // The PowerShell host provider name is defined in PowerShellHostProvider.cs
            const string PowerShellHostProviderName = "NuGetConsole.Host.PowerShell";

            var componentModel = ServiceLocator.GetGlobalService<SComponentModel, IComponentModel>();
            var exportProvider = componentModel.DefaultExportProvider;
            var hostProviderExports = exportProvider.GetExports<IHostProvider, IHostMetadata>();
            var psProvider = hostProviderExports.Single(export => export.Metadata.HostName == PowerShellHostProviderName);

            return psProvider.Value;
        }
    }
}
