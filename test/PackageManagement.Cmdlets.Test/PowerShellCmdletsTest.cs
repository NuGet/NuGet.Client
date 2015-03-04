using NuGet.Configuration;
using NuGet.PackageManagement;
using System;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Xunit;
using NuGet.Protocol.Core.Types;

namespace PackageManagement.Cmdlets.Test
{
    public class PowerShellCmdletsTest : IClassFixture<PowerShellCmdletsTestContext>
    {
        private SourceRepositoryProvider _provider;
        private PowerShellCmdletsTestContext _testContext;

        [Fact]
        public void TestPackageInstall()
        {
            bool success = RunScript("Install-Package", "jquery", "1.4.4");
            Assert.True(success);
        }

        private bool RunScript(string scriptText, params string[] parameters)
        {
            try
            {
                PowerShell ps = PowerShell.Create();
                ps.Runspace = _testContext.RunSpace;
                ps.Commands.AddCommand(scriptText);

                // Run the scriptText
                var testCommand = ps.Commands.Commands[0];
                testCommand.Parameters.Add("Id", parameters[0]);
                testCommand.Parameters.Add("Version", parameters[1]);
                // Add as a test hook to pass in the provider
                testCommand.Parameters.Add("Provider", _provider);

                // Add out-string
                ps.Commands.AddCommand("Out-String");

                // execute the script
                foreach (PSObject result in ps.Invoke())
                {
                    Console.WriteLine(result.ToString());
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public void SetFixture(PowerShellCmdletsTestContext data)
        {
            _testContext = data;
            ISettings settings = Settings.LoadDefaultSettings(Environment.ExpandEnvironmentVariables("%systemdrive%"), null, null);
            var packageSourceProvider = new PackageSourceProvider(settings);
            var packageSources = packageSourceProvider.LoadPackageSources();
            _provider = new SourceRepositoryProvider(packageSourceProvider, _testContext.ResourceProviders);
        }
    }
}
