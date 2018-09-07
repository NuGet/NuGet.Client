// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging;
using NuGet.ProjectModel;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Data model class to store deferred projects data.
    /// </summary>
    public sealed class DeferredProjectRestoreData
    {
        public DeferredProjectRestoreData(
            IReadOnlyDictionary<PackageReference, List<string>> packageReferenceDict,
            IReadOnlyList<PackageSpec> packageSpecs)
        {
            PackageReferenceDict = packageReferenceDict;
            PackageSpecs = packageSpecs;
        }

        public IReadOnlyDictionary<PackageReference, List<string>> PackageReferenceDict { get; }

        public IReadOnlyList<PackageSpec> PackageSpecs { get; }
    }
}
