using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI
{
    [Export(typeof(INuGetUIFactory))]
    public class NuGetUIFactory : INuGetUIFactory
    {

        public NuGetUIFactory()
        {
            // TODO: import options page
        }

        /// <summary>
        /// Returns the UI for the project or given set of projects.
        /// </summary>
        public INuGetUI Create(
            INuGetUIContext uiContext, 
            NuGetUIProjectContext uiProjectContext)
        {
            return new NuGetUI(uiContext, uiProjectContext);
        }
    }
}
