﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Validates the compatibility of a installed packages for the given project type. This
    /// component should be used after packages have been downloaded to disk but have not yet
    /// been installed to the project. If an installed package is not compatibile with the given
    /// project, an exception is thrown. The checks performed by this class are based on 
    /// package minimum client versions and package types.
    /// </summary>
    public interface IInstallationCompatibility
    {
        /// <summary>
        /// Validates the compatibility of a multiple installed packages for the given project type.
        /// </summary>
        /// <param name="nuGetProject">
        /// The NuGet project. The type of the NuGet project determines the sorts or validations that are done.
        /// </param>
        /// <param name="pathContext">The path context used to find the installed packages.</param>
        /// <param name="nuGetProjectActions">The project actions.</param>
        /// <param name="restoreResult">The restore result generated during installation.</param>
        void EnsurePackageCompatibility(
           NuGetProject nuGetProject,
           INuGetPathContext pathContext,
           IEnumerable<NuGetProjectAction> nuGetProjectActions,
           RestoreResult restoreResult);

        /// <summary>
        /// Validates the compatibility of a single downloaded package.
        /// </summary>
        /// <param name="nuGetProject">
        /// The NuGet project. The type of the NuGet project determines the sorts or validations that are done.
        /// </param>
        /// <param name="packageIdentity">The identity of that package contained in the download result.</param>
        /// <param name="resourceResult">The downloaded package.</param>
        void EnsurePackageCompatibility(
            NuGetProject nuGetProject,
            PackageIdentity packageIdentity,
            DownloadResourceResult resourceResult);
    }
}
