// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTEProject = EnvDTE.Project;
using NuGet.ProjectManagement;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using Microsoft.EnterpriseManagement.Configuration;
#if VisualStudioAuthoringExtensionsInstalled
using Microsoft.EnterpriseManagement.Packaging;
using System.Runtime.InteropServices;
using Microsoft.EnterpriseManagement.Configuration.IO;
#endif

namespace NuGet.PackageManagement.VisualStudio
{
    public class ManagementPackProjectSystem : VSMSBuildNuGetProjectSystem
    {
        private dynamic _projectMgr;
        private dynamic _mpReferenceContainerNode;

        //private dynamic _referenceContainerNode;

        public ManagementPackProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
            : base(envDTEProject, nuGetProjectContext)
        {
            _projectMgr = EnvDTEProject.Object;

            dynamic oaReferenceFolderItem = this.EnvDTEProject.ProjectItems.Item(1);

            // get the mp reference container node using reflection
            Type refFolderType = oaReferenceFolderItem.GetType();
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            PropertyInfo refFolderPropinfo = refFolderType.GetProperty("Node", bindingFlags);

            if (refFolderPropinfo != null)
            {
                _mpReferenceContainerNode = refFolderPropinfo.GetValue(oaReferenceFolderItem);
            }



        }

        public override void AddReference(string referencePath)
        {
#if !VisualStudioAuthoringExtensionsInstalled

            NuGetProjectContext.Log(MessageLevel.Info, "Visual Studio Authoring Extensions are not installed. Reference not added.");
            return;
#endif

            AddReferencesFromBundle(referencePath);

        }

        [Conditional("VisualStudioAuthoringExtensionsInstalled")]
        private void AddReferencesFromBundle(string bundlePath)
        {
            ManagementPackBundleReader bundleReader = ManagementPackBundleFactory.CreateBundleReader();
            var mpFileStore = new ManagementPackFileStore();
            try
            {
                ManagementPackBundle bundle = bundleReader.Read(bundlePath, new ManagementPackFileStore());

                foreach (var managementPack in bundle.ManagementPacks)
                {
                    try
                    {
                        if (!managementPack.Sealed)
                        {
                            LogProcessingResult(managementPack, ProcessStatus.NotSealed);
                        }

                        //ManagementPackReferenceNode packReferenceNode = new ManagementPackReferenceNode(_projectMgr, bundlePath, managementPack.Name);
                        //ReferenceNode existingEquivalentNode;
                        //if (packReferenceNode.IsAlreadyAdded(out existingEquivalentNode))
                        //{
                        //    packReferenceNode.Dispose();
                        //    LogProcessingResult(managementPack, ProcessStatus.AlreadyExists);
                        //}
                        //packReferenceNode.AddReference();
                        //LogProcessingResult(managementPack, ProcessStatus.Success);
                    }
                    catch (Exception ex)
                    {
                        LogProcessingResult(managementPack, ProcessStatus.Failed, ex.Message);
                    }
                }


            }
            catch (COMException ex)
            {
                NuGetProjectContext.Log(MessageLevel.Error, $"Unexpected COM exception while adding reference: {ex.ErrorCode}-{ex.Message}");
            }
            catch (Exception ex)
            {
                NuGetProjectContext.Log(MessageLevel.Error, $"Unexpected exception while adding reference: {ex.Message}");
            }
        }

        private void LogProcessingResult(ManagementPack managementPack, ProcessStatus status, string detail = null)
        {
            string identity = $"{managementPack.Name} (Version={managementPack.Version},PublicKeyToken={managementPack.KeyToken})";
            string result;
            switch (status)
            {
                case ProcessStatus.Success:
                    result = "added.";
                    break;
                case ProcessStatus.AlreadyExists:
                    result = "already exists.";
                    break;
                case ProcessStatus.NotSealed:
                    result = "is not sealed.";
                    break;
                default:
                    result = "failed while adding.";
                    break;
            }

            NuGetProjectContext.Log(MessageLevel.Info, $"{identity} {result} {detail}");
        }

        protected enum ProcessStatus
        {
            NotSealed,
            AlreadyExists,
            Success,
            Failed,
        }

        public override bool ReferenceExists(string name)
        {
            var referenceName = Path.GetFileNameWithoutExtension(name);

            foreach (dynamic reference in _mpReferenceContainerNode.EnumReferences())
            {
                if (String.Equals(reference.Name, referenceName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public override void RemoveReference(string name)
        {
            base.RemoveReference(name);
        }

        protected override bool IsBindingRedirectSupported
        {
            get
            {
                return false;
            }
        }

        public override string ProjectName
        {
            get
            {
                return base.ProjectName;
            }
        }

        public override string ProjectUniqueName
        {
            get
            {
                return base.ProjectUniqueName;
            }
        }
        protected override void AddGacReference(string name)
        {
            // We disable GAC references for Management Pack projects
        }

    }
}
