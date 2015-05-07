// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

extern alias Legacy;
using System.Collections.Generic;
using NuGet.Versioning;
using SemanticVersion = Legacy::NuGet.SemanticVersion;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    internal interface IPowerShellPackage
    {
        /// <summary>
        /// Id of the package
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// Versions of the package
        /// </summary>
        IEnumerable<NuGetVersion> Versions { get; set; }

        /// <summary>
        /// Semantic Version of the package
        /// Do not remove this property, it is needed for PS1 script backward-compatbility.
        /// </summary>
        SemanticVersion Version { get; set; }
    }
}
