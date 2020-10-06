// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    public interface INuGetUIContext : IDisposable
    {
        event EventHandler<IReadOnlyCollection<string>> ProjectActionsExecuted;

        IVsSolutionManager SolutionManager { get; }

        INuGetSolutionManagerService SolutionManagerService { get; }

        public INuGetSourcesService SourceService { get; }

        NuGetPackageManager PackageManager { get; }

        UIActionEngine UIActionEngine { get; }

        IPackageRestoreManager PackageRestoreManager { get; }

        IOptionsPageActivator OptionsPageActivator { get; }

        IEnumerable<IProjectContextInfo> Projects { get; set; }

        IUserSettingsManager UserSettingsManager { get; }

        IEnumerable<IVsPackageManagerProvider> PackageManagerProviders { get; }

        Task<bool> IsNuGetProjectUpgradeableAsync(IProjectContextInfo project, CancellationToken cancellationToken);

        Task<IModalProgressDialogSession> StartModalProgressDialogAsync(string caption, ProgressDialogData initialData, INuGetUI uiService);

        void RaiseProjectActionsExecuted(IReadOnlyCollection<string> projectIds);
    }
}
