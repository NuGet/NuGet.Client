// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using EnvDTEProject = EnvDTE.Project;
using NuGet.ProjectManagement;
using System;
using System.IO;
using System.Reflection;

//NOTE: Safely build a no-op proxy when VSAE is not installed (DON'T BREAK THE BUILD)
#if VisualStudioAuthoringExtensionsInstalled
using Microsoft.EnterpriseManagement.Configuration;
using Microsoft.EnterpriseManagement.Configuration.IO;
using Microsoft.EnterpriseManagement.Packaging;
using Microsoft.SystemCenter.Authoring.ProjectSystem;
using Microsoft.VisualStudio.Project;
#endif

namespace NuGet.PackageManagement.VisualStudio
{
    public interface IVsaeProjectSystemProxy
    {
        void AddManagementPackReferenceFromSealedMp(string sealedMpPath);
        void AddManagementPackReferencesFromBundle(string bundlePath);
        void RemoveManagementPackReference(string name);
        bool ReferenceExists(string name);
    }

    /// <summary>
    /// FUTURE:  We are highly leveraging Visual Studio Authoring Extensions (VSAE) for this project system.
    /// Unfortunately, we have to reflect some of the members needed at present.
    /// Our intention would be to engage with the VSAE team in order to make this cleaner and more fully supported.
    /// </summary>
    public class VsaeProjectSystemDynamicProxy : IVsaeProjectSystemProxy
    {
        private readonly dynamic _projectMgr;
        private readonly dynamic _mpReferenceContainerNode;
        private INuGetProjectContext _nuGetProjectContext;

#if VisualStudioAuthoringExtensionsInstalled
        private readonly MethodInfo _isAlreadyAddedMethodInfo;
        private delegate bool IsAlreadyAddedInternalDelegate(out ReferenceNode existingNodeInternal);
#endif

        public VsaeProjectSystemDynamicProxy(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
        {
            try
            {
                _projectMgr = envDTEProject.Object;
                _nuGetProjectContext = nuGetProjectContext;

                dynamic oaReferenceFolderItem = envDTEProject.ProjectItems.Item(1);

                // get the mp reference container node using reflection
                Type refFolderType = oaReferenceFolderItem.GetType();
                const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
                var refFolderPropinfo = refFolderType.GetProperty("Node", bindingFlags);
                _mpReferenceContainerNode = refFolderPropinfo.GetValue(oaReferenceFolderItem);

#if VisualStudioAuthoringExtensionsInstalled

                _isAlreadyAddedMethodInfo = typeof(ManagementPackReferenceNode).GetMethod("IsAlreadyAdded",
                    bindingFlags,
                    null,
                    new[] { typeof(ReferenceNode).MakeByRefType() },
                    null);
#endif
            }
            catch (Exception ex)
            {
                _nuGetProjectContext.Log(MessageLevel.Error, $"Unexpected exception in VsaeProjectSystemDynamicProxy ctor: {ex.Message}.");
                throw ex;
            }
        }

        public void AddManagementPackReferenceFromSealedMp(string sealedMpPath)
        {
#if VisualStudioAuthoringExtensionsInstalled

            using (var mpFileStore = new ManagementPackFileStore())
            {
                mpFileStore.AddDirectory(Path.GetDirectoryName(sealedMpPath));
                using (var managementPack = new ManagementPack(sealedMpPath, mpFileStore))
                {
                    AddManagementPackReference(managementPack, sealedMpPath);
                }
            }
#endif
        }

        public void AddManagementPackReferencesFromBundle(string bundlePath)
        {
#if VisualStudioAuthoringExtensionsInstalled

            var bundleReader = ManagementPackBundleFactory.CreateBundleReader();

            using (var mpFileStore = new ManagementPackFileStore())
            {
                var bundle = bundleReader.Read(bundlePath, mpFileStore);

                foreach (var managementPack in bundle.ManagementPacks) using (managementPack)
                    {
                        AddManagementPackReference(managementPack, bundlePath);
                    }
            }

#endif
        }

#if VisualStudioAuthoringExtensionsInstalled
        private void AddManagementPackReference(ManagementPack managementPack, string hintPath)
        {
            string identity = $"{managementPack.Name} (Version={managementPack.Version},PublicKeyToken={managementPack.KeyToken})";

            try
            {
                if (!managementPack.Sealed)
                {
                    LogProcessingResult(identity, ProcessStatus.NotSealed);
                    return;
                }

                using (var packReferenceNode = new ManagementPackReferenceNode(_projectMgr, hintPath, managementPack.Name))
                {
                    ReferenceNode existingEquivalentNode;

                    var isAlreadyAddedInternal = (IsAlreadyAddedInternalDelegate)Delegate.CreateDelegate(
                                                  typeof(IsAlreadyAddedInternalDelegate),
                                                  packReferenceNode,
                                                  _isAlreadyAddedMethodInfo);

                    if (isAlreadyAddedInternal(out existingEquivalentNode))
                    {
                        LogProcessingResult(identity, ProcessStatus.AlreadyExists);
                        existingEquivalentNode.Dispose();
                        return;
                    }

                    packReferenceNode.AddReference();
                    LogProcessingResult(identity, ProcessStatus.Success);
                }

            }
            catch (Exception ex)
            {
                LogProcessingResult(identity, ProcessStatus.Failed, ProcessingOp.AddingReference, ex.Message);
            }
        }
#endif

        private void LogProcessingResult(string identity, ProcessStatus status, ProcessingOp operation = ProcessingOp.AddingReference, string detail = null)
        {
            string result;
            switch (status)
            {
                case ProcessStatus.Success:
                    result = (operation == ProcessingOp.AddingReference) ? "added" : "removed";
                    break;
                case ProcessStatus.AlreadyExists:
                    result = "already exists";
                    break;
                case ProcessStatus.NotSealed:
                    result = "is not sealed";
                    break;
                case ProcessStatus.Failed:
                default:
                    result = $"failed while {((operation == ProcessingOp.AddingReference) ? "adding" : "removing")}";
                    break;
            }

            _nuGetProjectContext.Log(MessageLevel.Info, $"{identity} {result}. {detail}");
        }

        private enum ProcessStatus
        {
            NotSealed,
            AlreadyExists,
            Success,
            Failed,
        }

        private enum ProcessingOp
        {
            AddingReference,
            RemovingReference,
        }

        public void RemoveManagementPackReference(string name)
        {
            dynamic reference;
            if (!TryGetExistingReference(name, out reference)) return;
            using (reference)
            {
                var identity = reference.Name;
                var shouldRemoveFromStorage = false; // the nuget folder uninstall will take care of file cleanup
                reference.Remove(shouldRemoveFromStorage);
                LogProcessingResult(identity, ProcessStatus.Success);
            }
        }

        public bool ReferenceExists(string name)
        {
            var result = false;

            dynamic reference;
            if (TryGetExistingReference(name, out reference))
            {
                result = true;
                (reference as IDisposable)?.Dispose();
            }

            return result;
        }

        private bool TryGetExistingReference(string name, out dynamic managementPackReference)
        {
            var found = false;
            managementPackReference = null;

            var referenceName = Path.GetFileNameWithoutExtension(name);

            foreach (dynamic reference in _mpReferenceContainerNode.EnumReferences())
            {
                if (string.Equals(reference.Name, referenceName, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    managementPackReference = reference;
                    break;
                }

                (reference as IDisposable)?.Dispose();
            }

            return found;
        }

    }

    public class VsaeProjectSystemNoOpProxy : IVsaeProjectSystemProxy
    {
        private INuGetProjectContext _nuGetProjectContext;

        public VsaeProjectSystemNoOpProxy(INuGetProjectContext nuGetProjectContext)
        {
            _nuGetProjectContext = nuGetProjectContext;
        }

        public void AddManagementPackReferenceFromSealedMp(string sealedMpPath)
        {
            LogNotImplemented("AddManagementPackReferenceFromSealedMp");
        }

        public void AddManagementPackReferencesFromBundle(string bundlePath)
        {
            LogNotImplemented("AddManagementPackReferencesFromBundle");
        }

        public bool ReferenceExists(string name)
        {
            LogNotImplemented("ReferenceExists");
            return false;
        }

        public void RemoveManagementPackReference(string name)
        {
            LogNotImplemented("RemoveManagementPackReference");
        }

        private void LogNotImplemented(string methodName)
        {
            _nuGetProjectContext.Log(MessageLevel.Warning, $"{methodName} requires Visual Studio Authoring Extensions.");
        }
    }

}
