using EnvDTE80;
using NuGet.ProjectManagement;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace NuGet.PackageManagement.VisualStudio.SourceControl
{
    [Export(typeof(ITFSSourceControlManagerProvider))]
    public class DefaultTFSSourceControlManagerProvider : ITFSSourceControlManagerProvider
    {
        private readonly DefaultTFSSourceControlManager _sourceControlManager = new DefaultTFSSourceControlManager();
        public SourceControlManager GetTFSSourceControlManager(SourceControlBindings sourceControlBindings)
        {
            _sourceControlManager.SourceControlBindings = sourceControlBindings;
            return _sourceControlManager;
        }
    }

    internal class DefaultTFSSourceControlManager : SourceControlManager
    {
        internal SourceControlBindings SourceControlBindings { get; set; }
        public override void ProcessInstall(string root, IEnumerable<string> files)
        {
            // Do nothing in the default one
        }

        public override void CheckoutIfExists(string fullPath)
        {
            if(SourceControlBindings != null)
            {
                EnvDTEProjectUtility.EnsureCheckedOutIfExists(SourceControlBindings.Parent, fullPath);
            }
        }
    }
}
