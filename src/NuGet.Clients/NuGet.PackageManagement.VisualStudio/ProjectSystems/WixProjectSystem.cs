// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.ProjectManagement;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    public class WixProjectSystem : VSMSBuildNuGetProjectSystem
    {
        public WixProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
            : base(envDTEProject, nuGetProjectContext)
        {
        }

        private const string RootNamespace = "RootNamespace";
        private const string OutputName = "OutputName";
        private const string DefaultNamespace = "WiX";

        public override void AddReference(string referencePath)
        {
            // References aren't allowed for WiX projects
        }

        protected override void AddGacReference(string name)
        {
            // GAC references aren't allowed for WiX projects
        }

        protected override bool ExcludeFile(string path)
        {
            // Exclude nothing from WiX projects
            return false;
        }

        public override dynamic GetPropertyValue(string propertyName)
        {
            if (propertyName.Equals(RootNamespace, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return base.GetPropertyValue(OutputName);
                }
                catch
                {
                    return DefaultNamespace;
                }
            }
            return base.GetPropertyValue(propertyName);
        }

        public override void RemoveReference(string name)
        {
            // References aren't allowed for WiX projects
        }

        public override bool ReferenceExists(string name)
        {
            // References aren't allowed for WiX projects
            return true;
        }

        public override bool IsSupportedFile(string path)
        {
            return true;
        }
    }
}
