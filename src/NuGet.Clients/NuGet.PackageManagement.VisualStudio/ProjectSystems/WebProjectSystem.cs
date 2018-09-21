// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.ProjectManagement;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    public class WebProjectSystem : VSMSBuildNuGetProjectSystem
    {
        public WebProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
            : base(envDTEProject, nuGetProjectContext)
        {
        }

        public override bool IsSupportedFile(string path)
        {
            string fileName = Path.GetFileName(path);
            return !(fileName.StartsWith("app.", StringComparison.OrdinalIgnoreCase) &&
                     fileName.EndsWith(".config", StringComparison.OrdinalIgnoreCase));
        }
    }
}
