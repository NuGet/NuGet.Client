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


        public override bool ReferenceExists(string name)
        {
            var result = false;

            try
            {
                result = _proxy.ReferenceExists(name);
            }
            catch (Exception ex)
            {
                NuGetProjectContext.Log(MessageLevel.Error, $"Unexpected exception in ReferenceExists: {ex.Message}.");
            }

            return result;
        }


        public override void RemoveReference(string name)
        {
            try
            {
                _proxy.RemoveManagementPackReference(name);
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
