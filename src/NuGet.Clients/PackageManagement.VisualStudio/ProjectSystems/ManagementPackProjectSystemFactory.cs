// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.ProjectManagement;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class ManagementPackProjectSystemFactory
    {
        public static IMSBuildNuGetProjectSystem CreateManagementPackProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
        {
            IVsaeProjectSystemProxy proxy;
            try
            {
                // we're assuming we'd get an assembly load exception if VSAE does not exist at developer runtime 
                // this could be overkill, since we presumably wouldn't have the project type to initiate this workflow
                proxy = new VsaeProjectSystemDynamicProxy(envDTEProject, nuGetProjectContext);
            }
            catch
            {
                proxy = new VsaeProjectSystemNoOpProxy(nuGetProjectContext);
            }

            return new ManagementPackProjectSystem(envDTEProject, nuGetProjectContext, proxy);
        }
    }
}