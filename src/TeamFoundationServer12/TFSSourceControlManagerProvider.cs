using EnvDTE80;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;

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
        }
        private SourceControlBindings SourceControlBindings { get; set; }

        public override void AddFiles(string root, IEnumerable<string> files)
        {
            Debug.Assert(SourceControlBindings != null);
            DTESourceControlUtility.AddOrCheckoutItems(SourceControlBindings.Parent, files);
        }

        public override Stream CreateFile(string fullPath)
        {
            Debug.Assert(SourceControlBindings != null);
            bool fileNew = true;
            if (File.Exists(fullPath))
            {
                fileNew = false;
                DTESourceControlUtility.EnsureCheckedOutIfExists(SourceControlBindings.Parent, fullPath);
            }

            var fileStream = FileSystemUtility.CreateFile(fullPath);
            if (fileNew)
            {
                DTESourceControlUtility.EnsureCheckedOutIfExists(SourceControlBindings.Parent, fullPath);
            }

            return fileStream;
        }

        public override void DeleteFile(string fullPath)
        {
            throw new NotImplementedException();
        }

        public override void DeleteFiles(string root, IEnumerable<string> files)
        {
            throw new NotImplementedException();
        }

        public override void AddFilesUnderDirectory(string root)
        {
            // Only add files to Source Control
            var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories);
            AddFiles(root, files);
        }

        public override void DeleteFilesUnderDirectory(string root)
        {
            throw new NotImplementedException();
        }
    }
}
