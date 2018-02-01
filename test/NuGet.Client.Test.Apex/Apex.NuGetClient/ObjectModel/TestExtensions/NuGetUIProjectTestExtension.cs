using System;
using System.ComponentModel.Composition;
using Microsoft.Test.Apex.VisualStudio.Shell;
using NuGetClientTestContracts;

namespace Apex.NuGetClient.ObjectModel.TestExtensions
{
    [Export(typeof(NuGetUIProjectTestExtension))]
    public class NuGetUIProjectTestExtension : NuGetBaseTestExtension<object, NuGetUIProjectTestExtensionVerifier>
    {
        private ApexTestUIProject uiproject;
        private TimeSpan timeout = TimeSpan.FromSeconds(5);

        private string argument;

        /// <summary>
        /// NuGet Package Manager command GUID
        /// </summary>
        private readonly Guid nugetPackageManagerCmdSet_guid = new Guid("25fd982b-8cae-4cbd-a440-e03ffccde106");


        /// <summary>
        /// NuGet Package Manager identifier
        /// </summary>
        private readonly uint cmdidPackageManagerDialog = 0x100;

        [Import]
        public CommandingService Command { get; set; }

        public NuGetUIProjectTestExtension(ApexTestUIProject project)
        {
            uiproject = project;
        }

        /// <summary>
        /// Open Package Manager Window
        /// </summary>
        public void OpenPackageManager()
        {
            try
            {
                this.Command.ExecuteCommand(this.nugetPackageManagerCmdSet_guid, cmdidPackageManagerDialog, argument);
            }
            catch (Exception ex)
            { }
        }
    }
}
