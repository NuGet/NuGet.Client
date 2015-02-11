using NuGet.PackageManagement.VisualStudio;
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
        private readonly ISourceControlManagerProvider _sourceControlManagerProvider;
        public VSAPIProjectContext()
        {
            _sourceControlManagerProvider = ServiceLocator.GetInstanceSafe<ISourceControlManagerProvider>();
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


        public ISourceControlManagerProvider SourceControlManagerProvider
        {
            get { return _sourceControlManagerProvider; }
        }
    }
}
