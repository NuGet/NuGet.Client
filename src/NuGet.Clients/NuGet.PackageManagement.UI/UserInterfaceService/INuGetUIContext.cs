// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    public interface INuGetUIContext
    {
        ISourceRepositoryProvider SourceProvider { get; }

        ISolutionManager SolutionManager { get; }

        NuGetPackageManager PackageManager { get; }

        UIActionEngine UIActionEngine { get; }

        IPackageRestoreManager PackageRestoreManager { get; }

        IOptionsPageActivator OptionsPageActivator { get; }

        IEnumerable<NuGetProject> Projects { get; set; }

        IUserSettingsManager UserSettingsManager { get; }

        IEnumerable<IVsPackageManagerProvider> PackageManagerProviders { get; }
    }
}
