// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using EnvDTEProject = EnvDTE.Project;
using NuGet.ProjectManagement;
using System.IO;

namespace NuGet.PackageManagement.VisualStudio
{
    public class ManagementPackProjectSystem : VSMSBuildNuGetProjectSystem
    {
        private const string ManagementPackBundleExtension = ".mpb";
        private const string SealedManagementPackExtension = ".mp";

        private IVsaeProjectSystemProxy _proxy;

        public ManagementPackProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext, IVsaeProjectSystemProxy vsaeProxy)
            : base(envDTEProject, nuGetProjectContext)
        {
            _proxy = vsaeProxy;
        }

        public override void AddReference(string referencePath)
        {
            try
            {
                var extension = Path.GetExtension(referencePath);

                switch (extension)
                {
                    case ManagementPackBundleExtension:
                        _proxy.AddManagementPackReferencesFromBundle(referencePath);
                        break;
                    case SealedManagementPackExtension:
                        _proxy.AddManagementPackReferenceFromSealedMp(referencePath);
                        break;
                    default:
                        NuGetProjectContext.Log(MessageLevel.Warning, $"Unexpected reference extension ({extension}). Skipping AddReference.");
                        break;
                }
            }
            catch (Exception ex)
            {
                NuGetProjectContext.Log(MessageLevel.Error, $"Unexpected exception in AddReference: {ex.Message}.");
            }
        }

        public override bool ReferenceExists(string referencePath)
        {
            // due to the option and complexity of MP bundles, we skip this optimization and test during add reference
            return false;
        }

        public override void RemoveReference(string referencePath)
        {
            try
            {
                var extension = Path.GetExtension(referencePath);

                switch (extension)
                {
                    case ManagementPackBundleExtension:
                        _proxy.RemoveManagementPackReferencesFromBundle(referencePath);
                        break;
                    case SealedManagementPackExtension:
                        _proxy.RemoveManagementPackReferenceFromSealedMp(referencePath);
                        break;
                    default:
                        NuGetProjectContext.Log(MessageLevel.Warning, $"Unexpected reference extension ({extension}). Skipping RemoveReference.");
                        break;
                }
            }
            catch (Exception ex)
            {
                NuGetProjectContext.Log(MessageLevel.Error, $"Unexpected exception in RemoveReference: {ex.Message}.");
            }
        }

        protected override bool IsBindingRedirectSupported
        {
            get
            {
                return false;
            }
        }

        protected override void AddGacReference(string name)
        {
            // We disable GAC references for Management Pack projects
        }

    }
}
