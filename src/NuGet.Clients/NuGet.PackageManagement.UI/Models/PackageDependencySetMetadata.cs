// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;

namespace NuGet.PackageManagement.UI
{
    public class PackageDependencySetMetadata
    {
        public PackageDependencySetMetadata(PackageDependencyGroup dependencyGroup)
        {
            if (dependencyGroup == null)
            {
                IsNoDependencyPlaceHolder = true;
            }
            else
            {
                TargetFramework = dependencyGroup.TargetFramework;
                if (dependencyGroup.Packages.Any())
                {
                    Dependencies = dependencyGroup.Packages
                        .Select(d => new PackageDependencyMetadata(d))
                        .ToList()
                        .AsReadOnly();
                }
                else
                {
                    // There are no dependencies, instead return a collection with a special place holder, so UI can easily display "No Dependencies"
                    var dependencyPlaceHolder = new PackageDependencyMetadata() { IsNoDependencyPlaceHolder = true };
                    var dependenciesList = new List<PackageDependencyMetadata>();
                    dependenciesList.Add(dependencyPlaceHolder);
                    Dependencies = dependenciesList.AsReadOnly();
                }
            }
        }

        public NuGetFramework TargetFramework { get; private set; }
        public IReadOnlyCollection<PackageDependencyMetadata> Dependencies { get; private set; }
        public bool IsNoDependencyPlaceHolder { get; set; }
        public string TargetFrameworkDisplay
        {
            get
            {
                if (IsNoDependencyPlaceHolder)
                {
                    return Resources.Text_NoDependencies;
                }
                else
                {
                    return TargetFramework.ToString();
                }    
            }
        }
    }
}
