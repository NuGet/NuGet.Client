using EnvDTE80;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace NuGet.TeamFoundationServer
{
    [Export(typeof(ITFSSourceControlManagerProvider))]
    public class TFSSourceControlManagerProvider : ITFSSourceControlManagerProvider
    {
        private readonly ISettings _settings;

        [ImportingConstructor]
        public TFSSourceControlManagerProvider()
        {
            _settings = ServiceLocator.GetInstanceSafe<ISettings>();
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
        public DefaultTFSSourceControlManager(ISettings settings, SourceControlBindings sourceControlBindings) : base(settings)
        {
            if(sourceControlBindings == null)
            {
                throw new ArgumentNullException("sourceControlBindings");
            }
            SourceControlBindings = sourceControlBindings;
            TfsTeamProjectCollection projectCollection = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(new Uri(sourceControlBindings.ServerName));
            var versionControl = projectCollection.GetService<VersionControlServer>();
            PrivateWorkspace = versionControl.TryGetWorkspace(sourceControlBindings.LocalBinding);
        }
        private SourceControlBindings SourceControlBindings { get; set; }
        private Workspace PrivateWorkspace { get; set; }

        public override void AddFiles(string root, IEnumerable<string> files, INuGetProjectContext nuGetProjectContext)
        {
            IEnumerable<string> fullPaths = files.Select(f => FileSystemUtility.GetFullPath(root, f));
            AddFiles(fullPaths, nuGetProjectContext);
        }

        private void AddFiles(IEnumerable<string> fullPaths, INuGetProjectContext nuGetProjectContext)
        {
            HashSet<string> filesToAdd = new HashSet<string>();
            foreach (var fullPath in fullPaths)
            {
                if (File.Exists(fullPath))
                {
                    nuGetProjectContext.Log(MessageLevel.Warning, NuGet.ProjectManagement.Strings.Warning_FileAlreadyExists, fullPath);
                }

                // TODO: Should one also add the Directory under which the file is present since it is TFS?
                // It would be consistent across Source Control providers to only add files to Source Control

                filesToAdd.Add(fullPath);
            }

            if (filesToAdd.Count > 0)
            {
                PrivateWorkspace.PendAdd(filesToAdd.ToArray(), isRecursive: false);
            }    
        }

        public override Stream CreateFile(string fullPath, INuGetProjectContext nuGetProjectContext)
        {
            bool fileNew = true;
            if (File.Exists(fullPath))
            {
                fileNew = false;
                PrivateWorkspace.PendEdit(fullPath);
            }

            var fileStream = FileSystemUtility.CreateFile(fullPath);
            if (fileNew)
            {
                PrivateWorkspace.PendAdd(fullPath);
            }

            return fileStream;
        }

        public override void DeleteFile(string fullPath, INuGetProjectContext nuGetProjectContext)
        {
            throw new NotImplementedException();
        }

        public override void DeleteFiles(string root, IEnumerable<string> files, INuGetProjectContext nuGetProjectContext)
        {
            IEnumerable<string> fullPaths = files.Select(f => FileSystemUtility.GetFullPath(root, f));
            DeleteFiles(fullPaths, nuGetProjectContext);
        }

        private void DeleteFiles(IEnumerable<string> fullPaths, INuGetProjectContext nuGetProjectContext)
        {
            HashSet<string> filesToPendDelete = new HashSet<string>();
            foreach (var fullPath in fullPaths)
            {
                if (File.Exists(fullPath))
                {
                    //if (FileSystemExtensions.ContentEqual(this, fullPath, fullPath.GetStream))
                    //{
                    //    filesToPendDelete.Add(fullPath);
                    //}
                    //else
                    //{
                    //    nuGetProjectContext.Log(MessageLevel.Warning, NuGet.ProjectManagement.Strings.Warning_FileModified, fullPath);
                    //}
                    filesToPendDelete.Add(fullPath);
                }
            }
            PrivateWorkspace.PendDelete(filesToPendDelete.ToArray(), RecursionType.None);
        }

        public override void AddFilesUnderDirectory(string root, INuGetProjectContext nuGetProjectContext)
        {
            // Only add files to Source Control
            var fullPaths = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories);
            AddFiles(fullPaths, nuGetProjectContext);
        }

        public override void DeleteFilesUnderDirectory(string root, INuGetProjectContext nuGetProjectContext)
        {
            // Only add files to Source Control
            var fullPaths = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories);
            DeleteFiles(fullPaths, nuGetProjectContext);
        }
    }
}
