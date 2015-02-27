using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI
{
    public sealed class ProjectContext : INuGetProjectContext
    {
        public ProjectContext()
        {

        }

        public void Log(MessageLevel level, string message, params object[] args)
        {
            // TODO: log to the console
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            // TODO: prompt

            return FileConflictAction.Ignore;
        }

        public Packaging.PackageExtractionContext PackageExtractionContext
        {
            get;
            set;
        }


        public ISourceControlManagerProvider SourceControlManagerProvider
        {
            get { return null; }
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
