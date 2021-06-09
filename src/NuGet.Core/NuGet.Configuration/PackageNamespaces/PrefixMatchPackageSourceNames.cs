// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Configuration
{
    public class PrefixMatchPackageSourceNames
    {
        /// <summary>
        /// Generates a <see cref="PrefixMatchPackageSourceNames"/> based on the constructor params.
        /// </summary>
        /// <param name="packageNamespaceSectionPresent">If package namespace section present in nuget.config file</param>
        /// <param name="packageSourceNames">Package source names with prefixes matching search term</param>
        public PrefixMatchPackageSourceNames(bool packageNamespaceSectionPresent, HashSet<string> packageSourceNames)
        {
            PackageNamespaceSectionPresent = packageNamespaceSectionPresent;
            PackageSourceNames = packageSourceNames;
        }

        public bool PackageNamespaceSectionPresent { get;}
        public HashSet<string> PackageSourceNames { get;}
    }
}
