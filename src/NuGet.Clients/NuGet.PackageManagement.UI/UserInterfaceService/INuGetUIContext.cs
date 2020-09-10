// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    public interface INuGetUIContext : IDisposable
    {
        event EventHandler<IReadOnlyCollection<string>> ProjectActionsExecuted;

        ISourceRepositoryProvider SourceProvider { get; }

        IVsSolutionManager SolutionManager { get; }

        INuGetSolutionManagerService SolutionManagerService { get; }

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
