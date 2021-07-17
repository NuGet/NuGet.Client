// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Telemetry;
using Microsoft.VisualStudio.Shell;
using Microsoft.TeamFoundation.Common.Internal;
using NuGetConsole;

namespace NuGetVSExtension
{
    [Export(typeof(IRenderReadMeMarkdownToolWindow))]
    public class RenderReadMeMarkdownToolWindow : IRenderReadMeMarkdownToolWindow
    {
        private readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        public RenderReadMeMarkdownToolWindow(
        [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task DisplayReadMeMarkdownToolWindowAsync(string filePath)
        {
            var service = _serviceProvider.GetService(typeof(IReadMeMarkdownToolWindowOpener));
            if (service is IReadMeMarkdownToolWindowOpener package)
            {
                await package.ShowReadMeMarkdownToolWindowAsync(filePath);
            }
       }
    }
}
