// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;

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
            IEnumerable<NuGetProject> projects)
            :
                base(sourceProvider, solutionManager, packageManager, uiActionEngine, packageRestoreManager, optionsPageActivator, projects)
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
    }
}
