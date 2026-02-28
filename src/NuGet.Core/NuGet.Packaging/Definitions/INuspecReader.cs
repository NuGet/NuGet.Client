// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Packaging.Core;

namespace NuGet.Packaging
{
    /// <summary>
    /// A development package nuspec reader
    /// </summary>
    [Obsolete("This interface is unused and never implemented by any class. It will be removed in a future release.")]
    public interface INuspecReader : INuspecCoreReader
    {
        IEnumerable<PackageDependencyGroup> GetDependencyGroups();

        IEnumerable<FrameworkSpecificGroup> GetReferenceGroups();

        IEnumerable<FrameworkSpecificGroup> GetFrameworkReferenceGroups();

        /// <summary>
        /// The locale ID for the package, such as en-us.
        /// </summary>
        string GetLanguage();
    }
}
