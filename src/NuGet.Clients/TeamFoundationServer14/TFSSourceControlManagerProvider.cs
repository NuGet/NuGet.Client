// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using EnvDTE80;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;

namespace NuGet.TeamFoundationServer
{
    [Export(typeof(ITFSSourceControlManagerProvider))]
    public class TFSSourceControlManagerProvider : ITFSSourceControlManagerProvider
    {
        private readonly Configuration.ISettings _settings;

        [ImportingConstructor]
        public TFSSourceControlManagerProvider()
        {
            _settings = ServiceLocator.GetInstanceSafe<Configuration.ISettings>();
        }

        public SourceControlManager GetTFSSourceControlManager(SourceControlBindings sourceControlBindings)
        {
            if (_settings != null)
            {
                return new DefaultTFSSourceControlManager(_settings, sourceControlBindings);
            }
            return null;
        }
    }

    internal class DefaultTFSSourceControlManager : SourceControlManager
    {
        public DefaultTFSSourceControlManager(Configuration.ISettings settings, SourceControlBindings sourceControlBindings)
            : base(settings)
        {
            if (sourceControlBindings == null)
            {
                throw new ArgumentNullException(nameof(sourceControlBindings));
            }
            TfsTeamProjectCollection projectCollection = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(new Uri(sourceControlBindings.ServerName));
            var versionControl = projectCollection.GetService<VersionControlServer>();
            PrivateWorkspace = versionControl.TryGetWorkspace(sourceControlBindings.LocalBinding);
        }

        private Workspace PrivateWorkspace { get; set; }

        [SuppressMessage("Microsoft.Reliability", "CA2000")]
        public override Stream CreateFile(string fullPath, INuGetProjectContext nuGetProjectContext)
        {
            // See if there are any pending changes for this file
            var pendingChanges = PrivateWorkspace.GetPendingChanges(fullPath, RecursionType.None).ToArray();
            var pendingDeletes = pendingChanges.Where(c => c.IsDelete).ToArray();

            // We would need to pend an edit if (a) the file is pending delete (b) is bound to source control and does not already have pending edits or adds
            bool sourceControlBound = IsSourceControlBound(fullPath);
            bool requiresEdit = pendingDeletes.Any() || (!pendingChanges.Any(c => c.IsEdit || c.IsAdd) && sourceControlBound);

            // Undo all pending deletes
            if (pendingDeletes.Any())
            {
                PrivateWorkspace.Undo(pendingDeletes);
            }

            // If the file was marked as deleted, and we undid the change or has no pending adds or edits, we need to edit it.
            if (requiresEdit)
            {
                // If the file exists, but there is not pending edit then edit the file (if it is under source control)
                requiresEdit = PrivateWorkspace.PendEdit(fullPath) > 0;
            }

            var fileStream = FileSystemUtility.CreateFile(fullPath);
            // If we didn't have to edit the file, this must be a new file.
            if (!sourceControlBound)
            {
                PrivateWorkspace.PendAdd(fullPath);
            }

            return fileStream;
        }

        public override void PendAddFiles(IEnumerable<string> fullPaths, string root, INuGetProjectContext nuGetProjectContext)
        {
            HashSet<string> filesToAdd = new HashSet<string>();
            foreach (var fullPath in fullPaths)
            {
                // TODO: Should one also add the Directory under which the file is present since it is TFS?
                // It would be consistent across Source Control providers to only add files to Source Control
                filesToAdd.Add(fullPath);

                var directoryName = Path.GetDirectoryName(fullPath);
                if (!String.IsNullOrEmpty(directoryName))
                {
                    filesToAdd.Add(directoryName);
                }
            }

            ProcessAddFiles(filesToAdd, root);

            if (filesToAdd.Any())
            {
                PrivateWorkspace.PendAdd(filesToAdd.ToArray(), isRecursive: false);
            }
        }

        private void ProcessAddFiles(IEnumerable<string> fullPaths, string root)
        {
            if (!fullPaths.Any()
                || String.IsNullOrEmpty(root))
            {
                // Short-circuit if nothing specified
                return;
            }

            var batchSet = new HashSet<string>(fullPaths, StringComparer.OrdinalIgnoreCase);
            var batchFolders = batchSet.Select(Path.GetDirectoryName)
                .Distinct()
                .ToArray();

            // Prior to installing, we'll look at the directories and make sure none of them have any pending deletes.
            var pendingDeletes = PrivateWorkspace.GetPendingChanges(root, RecursionType.Full)
                .Where(c => c.IsDelete);

            // Find pending deletes that are in the same path as any of the folders we are going to be adding.
            var pendingFolderDeletesToUndo = pendingDeletes.Where(delete => batchFolders.Any(f => PathUtility.IsSubdirectory(delete.LocalItem, f)))
                .ToArray();

            // Undo directory deletes.
            if (pendingFolderDeletesToUndo.Any())
            {
                PrivateWorkspace.Undo(pendingFolderDeletesToUndo);
            }

            // Expand the directory deletes into individual file deletes. Include all the files we want to add but exclude any directories that may be in the path of the file.
            var childrenToPendDelete = (from folder in pendingFolderDeletesToUndo
                                        from childItem in GetItemsRecursive(folder.LocalItem)
                                        where batchSet.Contains(childItem) || !batchFolders.Any(f => PathUtility.IsSubdirectory(childItem, f))
                                        select childItem).ToArray();

            if (childrenToPendDelete.Any())
            {
                PrivateWorkspace.PendDelete(childrenToPendDelete, RecursionType.None);
            }

            // Undo exact file deletes
            var pendingFileDeletesToUndo = pendingDeletes.Where(delete =>
                fullPaths.Any(f =>
                    String.Equals(delete.LocalItem, ProjectManagement.PathUtility.ReplaceAltDirSeparatorWithDirSeparator(f), StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            if (pendingFileDeletesToUndo.Any())
            {
                PrivateWorkspace.Undo(pendingFileDeletesToUndo);
            }
        }

        private IEnumerable<string> GetItemsRecursive(string fullPath)
        {
            return PrivateWorkspace.VersionControlServer.GetItems(fullPath, Microsoft.TeamFoundation.VersionControl.Client.VersionSpec.Latest, RecursionType.Full, DeletedState.NonDeleted, ItemType.File)
                .Items.Select(i => PrivateWorkspace.TryGetLocalItemForServerItem(i.ServerItem));
        }

        private bool IsSourceControlBound(string fullPath)
        {
            try
            {
                var serverPath = PrivateWorkspace.TryGetServerItemForLocalItem(fullPath);
                return !String.IsNullOrEmpty(serverPath) && PrivateWorkspace.VersionControlServer.ServerItemExists(serverPath, ItemType.File);
            }
            catch (Exception)
            {
            }
            return false;
        }

        public override void PendDeleteFiles(IEnumerable<string> fullPaths, string root, INuGetProjectContext nuGetProjectContext)
        {
            HashSet<string> filesToPendDelete = new HashSet<string>();
            foreach (var fullPath in fullPaths)
            {
                if (File.Exists(fullPath))
                {
                    filesToPendDelete.Add(fullPath);
                }
            }

            // If the root is null or empty, simply try and pend delete on the fullpaths
            if (!String.IsNullOrEmpty(root))
            {
                // undo pending changes
                var pendingChanges = PrivateWorkspace.GetPendingChanges(
                    root, RecursionType.Full).ToArray();

                if (pendingChanges.Any())
                {
                    PrivateWorkspace.Undo(pendingChanges);
                }

                foreach (var pendingChange in pendingChanges)
                {
                    if (pendingChange.IsAdd)
                    {
                        // If a file was marked for add, it does not have to marked for delete
                        // Since, all the pending changes on the file are undone, no action needed here
                        filesToPendDelete.Remove(pendingChange.LocalItem);
                    }
                }
            }

            if (filesToPendDelete.Any())
            {
                PrivateWorkspace.PendDelete(filesToPendDelete.ToArray(), RecursionType.None);
            }
        }
    }
}