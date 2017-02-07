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
using Microsoft.EnterpriseManagement.Packaging;
using System.Runtime.InteropServices;
using Microsoft.EnterpriseManagement.Configuration.IO;

namespace NuGet.PackageManagement.VisualStudio
{
    public class ManagementPackProjectSystem : VSMSBuildNuGetProjectSystem
    {
        private dynamic _mpReferenceContainerNode;

        //private dynamic _referenceContainerNode;

        public ManagementPackProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext) 
            : base(envDTEProject, nuGetProjectContext)
        {
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

            NuGetProjectContext.Log(MessageLevel.Debug, "Visual Studio Authoring Extensions are not installed. Reference not added.");
            return;
#endif









        }


        private void AddReferencesFromBundle(string bundlePath)
        {
            //ManagementPackBundleReader bundleReader = ManagementPackBundleFactory.CreateBundleReader();
            //var mpFileStore = new ManagementPackFileStore();
            //try
            //{
            //    ManagementPackBundle bundle = bundleReader.Read(bundlePath, new ManagementPackFileStore());


            //    BundledItem[] array = bundle.ManagementPacks.Select<ManagementPack, BundledItem>((Func<ManagementPack, BundledItem>)(mp => this.ProcessBundleManagementPack(mp, bundlePath))).ToArray<BundledItem>();

            //    try
            //    {
            //        if (!managementPack.Sealed)
            //            return new BundledItem(managementPack, BundledItem.Status.NotSealed, (object)null);
            //        ManagementPackReferenceNode packReferenceNode = new ManagementPackReferenceNode(this.ProjectMgr, bundlePath, managementPack.Name);
            //        ReferenceNode existingEquivalentNode;
            //        if (packReferenceNode.IsAlreadyAdded(out existingEquivalentNode))
            //        {
            //            packReferenceNode.Dispose();
            //            return new BundledItem(managementPack, BundledItem.Status.AlreadyExists, (object)null);
            //        }
            //        packReferenceNode.AddReference();
            //        return new BundledItem(managementPack, BundledItem.Status.Success, (object)null);
            //    }
            //    catch (Exception ex)
            //    {
            //        return new BundledItem(managementPack, BundledItem.Status.Failed, (object)ex);
            //    }

            //}
            //catch (COMException ex)
            //{
            //    ErrorDialogHelper.ShowError(Resources.AddReferencesFromBundleFailed, (Exception)ex, (string)null, (Window)null, ErrorSeverity.Error, false);
            //    return ex.ErrorCode;
            //}
            //catch (Exception ex)
            //{
            //    ErrorDialogHelper.ShowError(Resources.AddReferencesFromBundleFailed, ex, (string)null, (Window)null, ErrorSeverity.Error, false);
            //    return -2147467259;
            //}
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
