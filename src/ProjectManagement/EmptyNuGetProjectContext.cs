using NuGet.Packaging;

namespace NuGet.ProjectManagement
{
    public class EmptyNuGetProjectContext : INuGetProjectContext
    {
        public void Log(MessageLevel level, string message, params object[] args)
        {
            // No-op
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

        public ExecutionContext ExecutionContext
        {
            get { return null; }
        }

        public void ReportError(string message)
        {   
        }
    }
}
