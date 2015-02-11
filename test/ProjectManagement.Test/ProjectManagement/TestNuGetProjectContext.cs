using NuGet.Packaging;
using NuGet.ProjectManagement;
using System;

namespace Test.Utility
{
    public class TestNuGetProjectContext : INuGetProjectContext
    {
        public void Log(MessageLevel level, string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            return FileConflictAction.IgnoreAll;
        }


        public PackageExtractionContext PackageExtractionContext
        {
            get;
            set;
        }

        public ISourceControlManagerProvider SourceControlManagerProvider
        {
            get
            {
                return null;
            }
        }
    }
}
