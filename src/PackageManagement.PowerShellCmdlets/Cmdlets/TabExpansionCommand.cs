using System.Management.Automation;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    /// <summary>
    /// This cmdlet will not be exported in the nuget module
    /// </summary>
    [Cmdlet("TabExpansion", "Package")]
    public class TabExpansionCommand : FindPackageCommand
    {
        [Parameter]
        public SwitchParameter ExcludeVersionInfo { get; set; }

        public TabExpansionCommand() : base() { }

        protected override void ProcessRecordCore()
        {
            base.Preprocess();

            // For tab expansion, only StartWith scenario is applicable
            base.FindPackageStartWithId(ExcludeVersionInfo);
        }
    }
}
