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

        void AddSettings(string key, UserSettings settings);

        UserSettings GetSettings(string key);

        // Persist settings
        void PersistSettings();

        /// <summary>
        /// Apply the setting of wether to show preview window to all existing
        /// package manager windows after user changes it by checking/unchecking the
        /// checkbox on the preview window.
        /// </summary>
        /// <param name="show">The value of the setting.</param>
        void ApplyShowPreviewSetting(bool show);

        IEnumerable<IVsPackageManagerProvider> PackageManagerProviders { get; }
    }
}
