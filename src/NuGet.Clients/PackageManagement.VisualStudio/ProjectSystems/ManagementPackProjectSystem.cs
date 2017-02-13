// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using EnvDTEProject = EnvDTE.Project;
using NuGet.ProjectManagement;
using System.Reflection;
using System.IO;

//NOTE: Safely build a no-op proxy when VSAE is not installed (DON'T BREAK THE BUILD)
#if VisualStudioAuthoringExtensionsInstalled
using Microsoft.EnterpriseManagement.Configuration;
using Microsoft.EnterpriseManagement.Configuration.IO;
using Microsoft.EnterpriseManagement.Packaging;
using System.Runtime.InteropServices;
using Microsoft.SystemCenter.Authoring.ProjectSystem;
using Microsoft.VisualStudio.Project;
#endif

namespace NuGet.PackageManagement.VisualStudio
{
    public class ManagementPackProjectSystem : VSMSBuildNuGetProjectSystem
    {
        private dynamic _projectMgr;
        private dynamic _mpReferenceContainerNode;

#if VisualStudioAuthoringExtensionsInstalled
        private MethodInfo _isAlreadyAddedMethodInfo;
        delegate bool IsAlreadyAddedInternalDelegate(out ReferenceNode existingNodeInternal);
#endif

        public ManagementPackProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
            : base(envDTEProject, nuGetProjectContext)
        {
            try
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
                NuGetProjectContext.Log(MessageLevel.Error, $"Unexpected exception in ctor: {ex.Message}.");
            }
        }

        public override void AddReference(string referencePath)
        {
            try
            {
                var extension = Path.GetExtension(referencePath);

                if (extension == ".mpb") // management pack bundle
                {
                    AddManagementPackReferencesFromBundle(referencePath);
                }
                else if (extension == ".mp") // sealed management pack
                {
                    AddManagementPackReferenceFromSealedMp(referencePath);
                }
                else
                {
                    NuGetProjectContext.Log(MessageLevel.Warning, $"Unexpected reference extension ({extension}). Skipping AddReference.");
                }
            }
            catch (Exception ex)
            {
                NuGetProjectContext.Log(MessageLevel.Error, $"Unexpected exception in AddReference: {ex.Message}.");
            }
        }

        private void AddManagementPackReferenceFromSealedMp(string sealedMpPath)
        {
#if VisualStudioAuthoringExtensionsInstalled

            try
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
            catch (COMException ex)
            {
                NuGetProjectContext.Log(MessageLevel.Error, $"Unexpected COM exception while adding reference: {ex.ErrorCode}-{ex.Message}");
            }
            catch (Exception ex)
            {
                NuGetProjectContext.Log(MessageLevel.Error, $"Unexpected exception while adding reference: {ex.Message}");
            }
#endif
        }

        private void AddManagementPackReferencesFromBundle(string bundlePath)
        {
#if VisualStudioAuthoringExtensionsInstalled

            ManagementPackBundleReader bundleReader = ManagementPackBundleFactory.CreateBundleReader();

            try
            {
                using (var mpFileStore = new ManagementPackFileStore())
                {
                    ManagementPackBundle bundle = bundleReader.Read(bundlePath, mpFileStore);

                    foreach (var managementPack in bundle.ManagementPacks) using (managementPack)
                        {
                            AddManagementPackReference(managementPack, bundlePath);
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
                default:
                    result = $"failed while {((operation == ProcessingOp.AddingReference) ? "adding" : "removing")}";
                    break;
            }

            NuGetProjectContext.Log(MessageLevel.Info, $"{identity} {result}. {detail}");
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

        public override bool ReferenceExists(string name)
        {
            bool result = false;

            try
            {
                dynamic reference;
                if(TryGetExistingReference(name, out reference))
                {
                    result = true;
                    (reference as IDisposable)?.Dispose();
                }
            }
            catch (Exception ex)
            {
                NuGetProjectContext.Log(MessageLevel.Error, $"Unexpected exception in ReferenceExists: {ex.Message}.");
            }

            return result;
        }

        private bool TryGetExistingReference(string name, out dynamic managementPackReference)
        {
            bool found = false;
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
                else
                {
                    (reference as IDisposable)?.Dispose();
                }
            }

            return found;
        }

        public override void RemoveReference(string name)
        {
            try
            {
                RemoveManagementPackReference(name);
            }
            catch (Exception ex)
            {
                NuGetProjectContext.Log(MessageLevel.Error, $"Unexpected exception in RemoveReference: {ex.Message}.");
            }
        }

        private void RemoveManagementPackReference(string name)
        {
            dynamic reference;
            if (TryGetExistingReference(name, out reference)) using (reference)
                {
                    var identity = reference.Name;
                    try
                    {
                        bool shouldRemoveFromStorage = false; // the nuget folder uninstall will take care of file cleanup
                        reference.Remove(shouldRemoveFromStorage);
                        LogProcessingResult(identity, ProcessStatus.Success);
                    }
                    catch (Exception ex)
                    {
                        LogProcessingResult(identity, ProcessStatus.Failed, ProcessingOp.RemovingReference, ex.Message);
                    }
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
