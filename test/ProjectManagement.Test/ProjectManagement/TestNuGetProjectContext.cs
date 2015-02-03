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


        public NuGet.Packaging.PackageExtractionContext PackageExtractionContext
        {
            get;
            set;
        }
    }
}
