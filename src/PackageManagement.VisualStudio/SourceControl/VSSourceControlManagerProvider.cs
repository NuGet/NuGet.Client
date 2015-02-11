using EnvDTE;
using EnvDTE80;
using NuGet.ProjectManagement;
using System;
using System.ComponentModel.Composition;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(ISourceControlManagerProvider))]
    public class VSSourceControlManagerProvider : ISourceControlManagerProvider
    {
        private readonly DTE _dte;
        private readonly ITFSSourceControlManagerProvider _TFSSourceControlManagerProvider;
        private const string TfsProviderName = "{4CA58AB2-18FA-4F8D-95D4-32DDF27D184C}";

        [ImportingConstructor]
        public VSSourceControlManagerProvider()
        {
            _dte = ServiceLocator.GetInstanceSafe<DTE>();
            _TFSSourceControlManagerProvider = ServiceLocator.GetInstanceSafe<ITFSSourceControlManagerProvider>();
        }

        public SourceControlManager GetSourceControlManager()
        {
            if(_dte != null)
            {
                var sourceControl = (SourceControl2)_dte.SourceControl;
                if (sourceControl != null)
                {
                    SourceControlBindings sourceControlBinding = null;
                    try
                    {
                        // Get the binding for this solution
                        sourceControlBinding = sourceControl.GetBindings(_dte.Solution.FullName);
                    }
                    catch (NotImplementedException)
                    {
                        // Some source control providers don't bother to implement this.
                        // TFS might be the only one using it
                    }

                    if (sourceControlBinding != null && String.IsNullOrEmpty(sourceControlBinding.ProviderName) ||
                            !sourceControlBinding.ProviderName.Equals(TfsProviderName, StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    if(_TFSSourceControlManagerProvider != null)
                    {
                        return _TFSSourceControlManagerProvider.GetTFSSourceControlManager(sourceControlBinding);
                    }
                }
            }

            return null;
        }
    }
}
