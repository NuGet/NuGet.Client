using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.VisualStudio
{
    internal sealed class VSAPIProjectContext : INuGetProjectContext
    {
        public VSAPIProjectContext()
        {

        }

        public void Log(MessageLevel level, string message, params object[] args)
        {
            // TODO: log somewhere?
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            // TODO: is this correct for the API?
            return FileConflictAction.OverwriteAll;
        }


        public Packaging.PackageExtractionContext PackageExtractionContext
        {
            get;
            set;
        }
    }
}
