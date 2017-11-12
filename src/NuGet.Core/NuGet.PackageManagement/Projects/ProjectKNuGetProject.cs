// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace NuGet.ProjectManagement.Projects
{
    public abstract class ProjectKNuGetProjectBase : NuGetProject, INuGetIntegratedProject
    {
        protected DependencyVersionLookup _dependencyVersionLookup;
        public DependencyVersionLookup Lookup => _dependencyVersionLookup;

        protected ProjectKNuGetProjectBase()
        {
            _dependencyVersionLookup = new DependencyVersionLookup();
        }

        // TODO-PATO: Implement this
        public Task<DateTime?> GetAssetsFileTimestampIFExistsAsync()
        {
            return null;
        }

        // TODO-PATO: Implement this
        public Task<IReadOnlyList<PackageIdentity>> GetTopLevelDependencies()
        {
            return null;
        }

        // TODO-PATO: Implement this
        public Task<string> GetAssetsFilePathOrNullAsync()
        {
            return null;
        }

    }
}
