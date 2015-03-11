using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.VisualStudio
{
    internal sealed class VSAPIProjectContext : IMSBuildNuGetProjectContext
    {
        private bool _skipAssemblyReferences;
        private bool _bindingRedirectsDisabled;
        private readonly ISourceControlManagerProvider _sourceControlManagerProvider;

        public VSAPIProjectContext()
            : this(false, false)
        {

        }

        public VSAPIProjectContext(bool skipAssemblyReferences, bool bindingRedirectsDisabled)
        {
            _sourceControlManagerProvider = ServiceLocator.GetInstanceSafe<ISourceControlManagerProvider>();
            _skipAssemblyReferences = skipAssemblyReferences;
            _bindingRedirectsDisabled = bindingRedirectsDisabled;
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


        public Packaging.PackageExtractionContext PackageExtractionContext { get; set; }


        public ISourceControlManagerProvider SourceControlManagerProvider
        {
            get { return _sourceControlManagerProvider; }
        }

        public ExecutionContext ExecutionContext
        {
            get { return null; }
        }

        public bool SkipAssemblyReferences
        {
            get
            {
                return _skipAssemblyReferences;
            }
        }

        public bool BindingRedirectsDisabled
        {
            get
            {
                return _bindingRedirectsDisabled;
            }
        }

        public void ReportError(string message)
        {
            // no-op
            Debug.Fail(message);
        }
    }
}
