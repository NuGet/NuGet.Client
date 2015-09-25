// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.Shell;
using NuGet.ProjectManagement;
using VSLangProj;
using EnvDTEProject = EnvDTE.Project;
using EnvDTEProjectItem = EnvDTE.ProjectItem;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    public class FSharpProjectSystem : VSMSBuildNuGetProjectSystem
    {
        public FSharpProjectSystem(EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext)
            : base(envDTEProject, nuGetProjectContext)
        {
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We want to swallow this exception. Read the comment below")]
        protected override async Task AddFileToProjectAsync(string path)
        {
            try
            {
                await base.AddFileToProjectAsync(path);
            }
            catch
            {
                // HACK: The F# project system blows up with the following stack when calling AddFromFileCopy on a newly created folder:

                // at Microsoft.VisualStudio.FSharp.ProjectSystem.MSBuildUtilities.MoveFileToBottom(String relativeFileName, ProjectNode projectNode)
                // at Microsoft.VisualStudio.FSharp.ProjectSystem.FSharpProjectNode.MoveFileToBottomIfNoOtherPendingMove(String relativeFileName)
                // at Microsoft.VisualStudio.FSharp.ProjectSystem.ProjectNode.AddItem(UInt32 itemIdLoc, VSADDITEMOPERATION op, String itemName, UInt32 filesToOpen, String[] files, IntPtr dlgOwner, VSADDRESULT[] result)

                // But it still ends up working so we swallow the exception. We should follow up with F# people to find out what's going on.
            }
        }

        protected override void AddGacReference(string name)
        {
            Debug.Assert(ThreadHelper.CheckAccess());
            // The F# project system expects assemblies that start with * to be framework assemblies.
            base.AddGacReference("*" + name);
        }

        public override bool FileExistsInProject(string path)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    EnvDTEProjectItem projectItem = await EnvDTEProjectUtility.GetProjectItemAsync(EnvDTEProject, path);
                    return (projectItem != null);
                });
        }

        /// <summary>
        /// WORKAROUND:
        /// This override is in place to handle the case-sensitive call to Project.Object.References.Item
        /// There are certain assemblies where the AssemblyName and Assembly file name do not match in case
        /// And, this causes a mismatch. For more information, Refer to the RemoveReference of the base class
        /// </summary>
        /// <param name="name"></param>
        public override void RemoveReference(string name)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    RemoveReferenceCore(name, EnvDTEProjectUtility.GetReferences(EnvDTEProject));
                });
        }

        private void RemoveReferenceCore(string name, References references)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            try
            {
                var referenceName = Path.GetFileNameWithoutExtension(name);

                Reference reference = references.Item(referenceName);

                if (reference == null)
                {
                    // No exact match found for referenceName. Trying case-insensitive search
                    NuGetProjectContext.Log(ProjectManagement.MessageLevel.Warning, Strings.Warning_NoExactMatchForReference, referenceName);
                    foreach (Reference r in references)
                    {
                        if (String.Equals(referenceName, r.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            if (reference == null)
                            {
                                reference = r;
                            }
                            else
                            {
                                var message = String.Format(CultureInfo.CurrentCulture, Strings.FailedToRemoveReference, referenceName);
                                NuGetProjectContext.Log(ProjectManagement.MessageLevel.Error, message);
                                throw new InvalidOperationException(message);
                            }
                        }
                    }
                }

                // At this point, the necessary case-sensitive and case-insensitive search are performed
                if (reference != null)
                {
                    reference.Remove();
                    NuGetProjectContext.Log(ProjectManagement.MessageLevel.Debug, Strings.Debug_RemoveReference, name, ProjectName);
                }
                else
                {
                    NuGetProjectContext.Log(ProjectManagement.MessageLevel.Warning, Strings.Warning_FailedToFindMatchForRemoveReference, referenceName);
                }
            }
            catch (Exception e)
            {
                NuGetProjectContext.Log(ProjectManagement.MessageLevel.Warning, e.Message);
            }
        }
    }
}
