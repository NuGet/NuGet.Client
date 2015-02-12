using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.IO;

namespace NuGet.ProjectManagement
{
    public abstract class SourceControlManager
    {
        protected ISettings Settings { get; set; }
        protected SourceControlManager(ISettings settings)
        {
            if(settings == null)
            {
                throw new ArgumentNullException("settings");
            }
            Settings = settings;
        }        
        public abstract Stream CreateFile(string fullPath);
        public abstract void DeleteFile(string fullPath);
        public abstract void AddFiles(string root, IEnumerable<string> files);
        public abstract void AddFilesUnderDirectory(string root);
        public abstract void DeleteFiles(string root, IEnumerable<string> files);
        public abstract void DeleteFilesUnderDirectory(string root);

        public bool IsPackagesFolderBoundToSourceControl()
        {
            return !SourceControlUtility.IsSourceControlDisabled(Settings);
        }
    }
}
