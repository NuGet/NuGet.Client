using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Configuration;
using NuGet.ProjectManagement;

namespace Test.Utility
{
    public class TestSourceControlManager : SourceControlManager
    {
        public HashSet<string> PendAddedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> PendDeletedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public TestSourceControlManager() : base(NullSettings.Instance) { }

        public TestSourceControlManager(ISettings settings) : base(settings) { }

        public override Stream CreateFile(string fullPath, INuGetProjectContext nuGetProjectContext)
        {
            PendAddedFiles.Add(fullPath);
            return FileSystemUtility.CreateFile(fullPath);
        }

        public override void PendAddFiles(
            IEnumerable<string> fullPaths,
            string root,
            INuGetProjectContext nuGetProjectContext)
        {
            foreach(var path in fullPaths)
            {
                PendAddedFiles.Add(path);
            }
        }

        public override void PendDeleteFiles(
            IEnumerable<string> fullPaths,
            string root,
            INuGetProjectContext nuGetProjectContext)
        {
            foreach(var path in fullPaths)
            {
                PendDeletedFiles.Add(path);
            }
        }
    }
}
