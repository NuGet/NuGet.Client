using System;
using System.ComponentModel.Composition;
using Apex.NuGetClient.ObjectModel.TestExtensions;
using EnvDTE;
using Microsoft.Test.Apex.VisualStudio;
using Microsoft.Test.Apex.VisualStudio.Shell;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.VisualStudio;
using NuGetClientTestContracts;

namespace Apex.NuGetClient.TestServices
{
    [Export(typeof(NuGetApexTestService))]
    public class NuGetApexTestService : VisualStudioTestService<NuGetApexVerifier>
    {
        [Import]
        private NuGetApexUITestService NuGetApexUITestService { get; set; }

        /// <summary>
        /// Get or set the DTE commanding service
        /// </summary>
        [Import]
        public CommandingService Commanding { get; set; }

        /// <summary>
        /// NuGet Package Manager Window command Guid.
        /// </summary>
        private readonly Guid guidNuGetDialogCmdSet = new Guid("25fd982b-8cae-4cbd-a440-e03ffccde106");

        /// <summary>
        /// NuGet Package Manager command identifier.
        /// </summary>
        private const int cmdidAddPackageDialog = 0x100;

        protected internal DTE Dte
        {
            get
            {
                return this.VisualStudioObjectProviders.DTE;
            }
        }

        protected internal IVsPackageInstallerServices InstallerServices
        {
            get
            {
                return this.VisualStudioObjectProviders.GetComponentModelService<IVsPackageInstallerServices>();
            }
        }

        public NuGetUIProjectTestExtension GetUIWindowfromProject(ProjectTestExtension project)
        {
            string argument = null;

            try
            {
                this.Commanding.ExecuteCommand(guidNuGetDialogCmdSet, cmdidAddPackageDialog, argument);
            }
            catch(InvalidOperationException ex)
            {
                throw new InvalidOperationException(ex.Message);
            }

            var uiproject = NuGetApexUITestService.GetApexTestUIProject(project.Name);
            return new NuGetUIProjectTestExtension(uiproject);
        }
    }
}
