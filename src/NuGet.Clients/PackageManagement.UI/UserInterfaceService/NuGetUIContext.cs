// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
    public sealed class NuGetUIContext : INuGetUIContext
    {
        private NuGetProject[] _projects;

        public NuGetUIContext(
            ISourceRepositoryProvider sourceProvider,
            ISolutionManager solutionManager,
            NuGetPackageManager packageManager,
            UIActionEngine uiActionEngine,
            IPackageRestoreManager packageRestoreManager,
            IOptionsPageActivator optionsPageActivator,
            IUserSettingsManager userSettingsManager,
            IEnumerable<IVsPackageManagerProvider> packageManagerProviders)
        {
            SourceProvider = sourceProvider;
            SolutionManager = solutionManager;
            PackageManager = packageManager;
            UIActionEngine = uiActionEngine;
            PackageManager = packageManager;
            PackageRestoreManager = packageRestoreManager;
            OptionsPageActivator = optionsPageActivator;
            UserSettingsManager = userSettingsManager;
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
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _projects = value.ToArray();
            }
        }

        public IUserSettingsManager UserSettingsManager { get; }

        public IEnumerable<IVsPackageManagerProvider> PackageManagerProviders { get; }
    }
}
