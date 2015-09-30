// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;

namespace NuGetVSExtension
{
    internal class VisualStudioUIContext : NuGetUIContextBase
    {
        private NuGetPackage _package;

        public VisualStudioUIContext(
            NuGetPackage package,
            ISourceRepositoryProvider sourceProvider,
            ISolutionManager solutionManager,
            NuGetPackageManager packageManager,
            UIActionEngine uiActionEngine,
            IPackageRestoreManager packageRestoreManager,
            IOptionsPageActivator optionsPageActivator,
            IEnumerable<NuGetProject> projects,
            IEnumerable<IVsPackageManagerProvider> packageManagerProviders)
            :
                base(sourceProvider, solutionManager, packageManager, uiActionEngine, packageRestoreManager, optionsPageActivator, projects, packageManagerProviders)
        {
            _package = package;
        }

        public override UserSettings GetSettings(string key)
        {
            return _package.GetWindowSetting(key);
        }

        public override void AddSettings(string key, UserSettings obj)
        {
            _package.AddWindowSettings(key, obj);
        }

        public override void PersistSettings()
        {
            _package.SaveNuGetSettings();
        }

        public override void ApplyShowPreviewSetting(bool show)
        {
            var serviceProvider = ServiceLocator.GetInstance<IServiceProvider>();
            IVsUIShell uiShell = (IVsUIShell)serviceProvider.GetService(typeof(SVsUIShell));
            foreach (var windowFrame in VsUtility.GetDocumentWindows(uiShell))
            {
                var packageManagerControl = VsUtility.GetPackageManagerControl(windowFrame);
                if (packageManagerControl != null)
                {
                    packageManagerControl.ApplyShowPreviewSetting(show);
                }
            }
        }
    }
}