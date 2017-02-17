// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using EnvDTEProject = EnvDTE.Project;
using NuGet.ProjectManagement;
using System;
using System.IO;
using System.Reflection;
using System.Linq;

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
        void RemoveManagementPackReferenceFromSealedMp(string sealedMpPath);
        void RemoveManagementPackReferencesFromBundle(string bundlePath);
    }

#if VisualStudioAuthoringExtensionsInstalled
    /// <summary>
    /// FUTURE:  We are highly leveraging Visual Studio Authoring Extensions (VSAE) for this project system.
    /// Unfortunately, we have to reflect some of the members needed at present.
    /// Our intention would be to engage with the VSAE team in order to make this cleaner and more fully supported.
    /// </summary>
    public class VsaeProjectSystemDynamicProxy : IVsaeProjectSystemProxy
    {
        private readonly dynamic _projectMgr;
        private INuGetProjectContext _nuGetProjectContext;

        private readonly MethodInfo _isAlreadyAddedMethodInfo;
        private readonly ManagementPackReferenceContainerNode _mpReferenceContainerNode;
        private delegate bool IsAlreadyAddedInternalDelegate(out ReferenceNode existingNodeInternal);

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

                _mpReferenceContainerNode = (ManagementPackReferenceContainerNode)refFolderPropinfo.GetValue(oaReferenceFolderItem);

                _isAlreadyAddedMethodInfo = typeof(ManagementPackReferenceNode).GetMethod("IsAlreadyAdded",
                    bindingFlags,
                    null,
                    new[] { typeof(ReferenceNode).MakeByRefType() },
                    null);
            }
            catch (Exception ex)
            {
                _nuGetProjectContext.Log(MessageLevel.Error, $"Unexpected exception in VsaeProjectSystemDynamicProxy ctor: {ex.Message}.");
                throw ex;
            }
        }

        public void AddManagementPackReferenceFromSealedMp(string sealedMpPath)
        {
            using (var mpFileStore = new ManagementPackFileStore())
            {
                mpFileStore.AddDirectory(Path.GetDirectoryName(sealedMpPath));
                using (var managementPack = new ManagementPack(sealedMpPath, mpFileStore))
                {
                    AddManagementPackReference(managementPack, sealedMpPath);
                }
            }
        }

        public void AddManagementPackReferencesFromBundle(string bundlePath)
        {
            PerformOnManagementPacksInBundle(bundlePath, (mp, path) => AddManagementPackReference(mp, path));
        }

        private void PerformOnManagementPacksInBundle(string bundlePath, Action<ManagementPack, string> processManagementPack)
        {
            var bundleReader = ManagementPackBundleFactory.CreateBundleReader();

            using (var mpFileStore = new ManagementPackFileStore())
            {
                var bundle = bundleReader.Read(bundlePath, mpFileStore);

                foreach (var managementPack in bundle.ManagementPacks) using (managementPack)
                    {
                        processManagementPack(managementPack, bundlePath);
                    }
            }
        }

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

        private void LogProcessingResult(string identity, ProcessStatus status, ProcessingOp operation = ProcessingOp.AddingReference, string detail = "")
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

        public void RemoveManagementPackReferenceFromSealedMp(string sealedMpPath)
        {
            var referenceName = Path.GetFileNameWithoutExtension(sealedMpPath);

            SafeRemoveManagementPackReference(referenceName);
        }

        public void RemoveManagementPackReferencesFromBundle(string bundleName)
        {
            string fullPath;
            if (TryGetFullBundlePath(bundleName, out fullPath))
            {
                PerformOnManagementPacksInBundle(fullPath, (mp, path) => SafeRemoveManagementPackReference(mp.Name));
            }
        }

        private bool TryGetFullBundlePath(string bundleName, out string fullPath)
        {
            var found = false;
            fullPath = null;

            foreach (var reference in _mpReferenceContainerNode.EnumReferences().Cast<ManagementPackReferenceNode>()) using (reference)
                {
                    if (reference.ManagementPackPath.EndsWith(bundleName, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        fullPath = reference.ManagementPackPath;
                        break;
                    }
                }

            return found;
        }

        private void SafeRemoveManagementPackReference(string referenceName)
        {
            var found = false;
            ManagementPackReferenceNode managementPackReference = null;

            foreach (var reference in _mpReferenceContainerNode.EnumReferences().Cast<ManagementPackReferenceNode>())
            {
                if (string.Equals(reference.Name, referenceName, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    managementPackReference = reference;
                    break;
                }

                (reference as IDisposable)?.Dispose();
            }

            if (found)
            {
                using (managementPackReference)
                {
                    var identity = managementPackReference.Name;
                    var shouldRemoveFromStorage = false; // the nuget folder uninstall will take care of file cleanup
                    managementPackReference.Remove(shouldRemoveFromStorage);
                    LogProcessingResult(identity, ProcessStatus.Success, ProcessingOp.RemovingReference);
                }
            }
        }

    }
#endif

    public class VsaeProjectSystemNoOpProxy : IVsaeProjectSystemProxy
    {
        private INuGetProjectContext _nuGetProjectContext;

        public VsaeProjectSystemNoOpProxy(INuGetProjectContext nuGetProjectContext)
        {
            _nuGetProjectContext = nuGetProjectContext;
        }

        public void AddManagementPackReferenceFromSealedMp(string sealedMpPath)
        {
            LogNotImplemented("AddManagementPackReference");
        }

        public void AddManagementPackReferencesFromBundle(string bundlePath)
        {
            LogNotImplemented("AddManagementPackReference");
        }

        public void RemoveManagementPackReferenceFromSealedMp(string sealedMpPath)
        {
            LogNotImplemented("RemoveManagementPackReference");
        }

        public void RemoveManagementPackReferencesFromBundle(string bundlePath)
        {
            LogNotImplemented("RemoveManagementPackReference");
        }

        private void LogNotImplemented(string methodName)
        {
            _nuGetProjectContext.Log(MessageLevel.Warning, $"{methodName} requires Visual Studio Authoring Extensions.");
        }
    }

}
