// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement.VisualStudio;
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
            IVsSolutionManager solutionManager,
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

        public IVsSolutionManager SolutionManager { get; }

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

        public async  Task<bool> IsNuGetProjectUpgradeable(NuGetProject project)
        {
            return await NuGetProjectUpgradeHelper.IsNuGetProjectUpgradeableAsync(project);
        }

        [SuppressMessage("Microsoft.VisualStudio.Threading.Analyzers", "VSTHRD010", Justification = "NuGet/Home#4833 Baseline")]
        public IModalProgressDialogSession StartModalProgressDialog(string caption, ProgressDialogData initialData, INuGetUI uiService)
        {
            var waitForDialogFactory = (IVsThreadedWaitDialogFactory)Package.GetGlobalService(typeof(SVsThreadedWaitDialogFactory));
            var progressData = new ThreadedWaitDialogProgressData(
                initialData.WaitMessage,
                initialData.ProgressText,
                null,
                initialData.IsCancelable,
                initialData.CurrentStep,
                initialData.TotalSteps);
            var session = waitForDialogFactory.StartWaitDialog(caption, progressData);
            return new VisualStudioProgressDialogSession(session);
        }
    }
}
