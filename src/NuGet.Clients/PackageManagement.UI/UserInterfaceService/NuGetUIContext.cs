// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Context of a PackageManagement UI window
    /// </summary>
    public abstract class NuGetUIContextBase : INuGetUIContext
    {
        private NuGetProject[] _projects;

        protected NuGetUIContextBase(
            ISourceRepositoryProvider sourceProvider,
            ISolutionManager solutionManager,
            NuGetPackageManager packageManager,
            UIActionEngine uiActionEngine,
            IPackageRestoreManager packageRestoreManager,
            IOptionsPageActivator optionsPageActivator,
            IEnumerable<NuGetProject> projects,
            IEnumerable<IVsPackageManagerProvider> packageManagerProviders)
        {
            SourceProvider = sourceProvider;
            SolutionManager = solutionManager;
            PackageManager = packageManager;
            UIActionEngine = uiActionEngine;
            PackageManager = packageManager;
            PackageRestoreManager = packageRestoreManager;
            OptionsPageActivator = optionsPageActivator;
            _projects = projects.ToArray();
            PackageManagerProviders = packageManagerProviders;
        }

        public ISourceRepositoryProvider SourceProvider { get; }

        public ISolutionManager SolutionManager { get; }

        public NuGetPackageManager PackageManager { get; }

        public UIActionEngine UIActionEngine { get; }

        public IPackageRestoreManager PackageRestoreManager { get; }

        public IOptionsPageActivator OptionsPageActivator { get; }

        public IEnumerable<NuGetProject> Projects
        {
            get { return _projects; }
            set
            {
                _projects = value.ToArray();
            }
        }

        public abstract void AddSettings(string key, UserSettings settings);

        public abstract UserSettings GetSettings(string key);

        public abstract void PersistSettings();

        public abstract void ApplyShowPreviewSetting(bool show);

        public IEnumerable<IVsPackageManagerProvider> PackageManagerProviders { get; }
    }
}
