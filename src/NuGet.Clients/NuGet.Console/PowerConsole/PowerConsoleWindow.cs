// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace NuGetConsole.Implementation.PowerConsole
{
    [Export(typeof(IPowerConsoleWindow))]
    [Export(typeof(IHostInitializer))]
    internal class PowerConsoleWindow : IPowerConsoleWindow, IHostInitializer, IDisposable
    {
        public const string ContentType = "PackageConsole";

        private Dictionary<string, HostInfo> _hostInfos;
        private HostInfo _activeHostInfo;

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [Import(typeof(SVsServiceProvider))]
        internal IServiceProvider ServiceProvider { get; set; }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [Import]
        internal IWpfConsoleService WpfConsoleService { get; set; }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [ImportMany]
        internal IEnumerable<Lazy<IHostProvider, IHostMetadata>> HostProviders { get; set; }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope",
            Justification = "_hostInfo collection is disposed.")]
        private Dictionary<string, HostInfo> HostInfos
        {
            get
            {
                if (_hostInfos == null)
                {
                    _hostInfos = new Dictionary<string, HostInfo>();
                    foreach (Lazy<IHostProvider, IHostMetadata> p in HostProviders)
                    {
                        HostInfo info = new HostInfo(this, p);
                        _hostInfos[info.HostName] = info;
                    }
                }
                return _hostInfos;
            }
        }

        internal HostInfo ActiveHostInfo
        {
            get
            {
                if (_activeHostInfo == null)
                {
                    // we only have exactly one host, the PowerShellHost. So always choose the first and only one.
                    _activeHostInfo = HostInfos.Values.FirstOrDefault();
                }
                return _activeHostInfo;
            }
        }

        // represent the default feed
        public string ActivePackageSource
        {
            get
            {
                HostInfo hi = ActiveHostInfo;
                return (hi != null && hi.WpfConsole != null && hi.WpfConsole.Host != null) ?
                    ActiveHostInfo.WpfConsole.Host.ActivePackageSource :
                    null;
            }
            set
            {
                HostInfo hi = ActiveHostInfo;
                if (hi != null
                    && hi.WpfConsole != null
                    && hi.WpfConsole.Host != null)
                {
                    hi.WpfConsole.Host.ActivePackageSource = value;
                }
            }
        }

        public string[] PackageSources
        {
            get { return ActiveHostInfo.WpfConsole.Host.GetPackageSources(); }
        }

        public string[] AvailableProjects
        {
            get { return ActiveHostInfo.WpfConsole.Host.GetAvailableProjects(); }
        }

        public string DefaultProject
        {
            get
            {
                HostInfo hi = ActiveHostInfo;
                return (hi != null && hi.WpfConsole != null && hi.WpfConsole.Host != null) ?
                    ActiveHostInfo.WpfConsole.Host.DefaultProject :
                    String.Empty;
            }
        }

        public void SetDefaultProjectIndex(int selectedIndex)
        {
            HostInfo hi = ActiveHostInfo;
            if (hi != null
                && hi.WpfConsole != null
                && hi.WpfConsole.Host != null)
            {
                hi.WpfConsole.Host.SetDefaultProjectIndex(selectedIndex);
            }
        }

        [SuppressMessage("Microsoft.VisualStudio.Threading.Analyzers", "VSTHRD010", Justification = "NuGet/Home#4833 Baseline")]
        public void Show()
        {
            IVsUIShell vsUIShell = ServiceProvider.GetService<IVsUIShell>(typeof(SVsUIShell));
            if (vsUIShell != null)
            {
                Guid guid = typeof(PowerConsoleToolWindow).GUID;
                IVsWindowFrame frame;

                ErrorHandler.ThrowOnFailure(
                    vsUIShell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fForceCreate, ref guid, out frame));

                if (frame != null)
                {
                    ErrorHandler.ThrowOnFailure(frame.Show());
                }
            }
        }

        public void Start()
        {
            ActiveHostInfo.WpfConsole.Dispatcher.Start();
        }

        public void SetDefaultRunspace()
        {
            ActiveHostInfo.WpfConsole.Host.SetDefaultRunspace();
        }

        void IDisposable.Dispose()
        {
            if (_hostInfos != null)
            {
                foreach (IDisposable hostInfo in _hostInfos.Values)
                {
                    if (hostInfo != null)
                    {
                        hostInfo.Dispose();
                    }
                }
            }
        }
    }
}
