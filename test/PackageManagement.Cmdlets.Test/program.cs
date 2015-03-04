using NuGet.Configuration;
using NuGet.PackageManagement;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using Test.Utility;
using NuGet.Protocol.Core.Types;

namespace PackageManagement.Cmdlets.Test
{
    public class Program
    {
        [ImportMany]
        public IEnumerable<Lazy<INuGetResourceProvider>> ResourceProviders { get; set; }

        private Runspace _runSpace;

        static void Main(string[] args)
        {
            var p = new Program();
            p.InitializeComponents();
            p.ImportModule();
            p.InstallPackage();
        }

        void InitializeComponents()
        {
            var aggregateCatalog = new AggregateCatalog();
            {
                aggregateCatalog.Catalogs.Add(new DirectoryCatalog(Environment.CurrentDirectory, "*.dll"));
                var container = new CompositionContainer(aggregateCatalog);
                container.ComposeParts(this);
            }
        }

        private void InstallPackage()
        {
            try
            {
                RunScript("Install-Package", "entityframework", "5.0.0");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void ImportModule()
        {
            InitialSessionState initial = InitialSessionState.CreateDefault();
            string assemblyPath = Path.Combine(Environment.CurrentDirectory, @"..\..\..\..\src\PackageManagement.PowerShellCmdlets\bin\debug\NuGet.PackageManagement.PowerShellCmdlets.dll");
            initial.ImportPSModule(new string[] { assemblyPath });
            _runSpace = RunspaceFactory.CreateRunspace(initial);
            _runSpace.Open();
        }

        private void RunScript(string scriptText, params string[] parameters)
        {
            ISettings settings = Settings.LoadDefaultSettings(Environment.ExpandEnvironmentVariables("%systemdrive%"), null, null);
            var packageSourceProvider = new PackageSourceProvider(settings);
            var packageSources = packageSourceProvider.LoadPackageSources();
            SourceRepositoryProvider provider = new SourceRepositoryProvider(packageSourceProvider, ResourceProviders);

            PowerShell ps = PowerShell.Create();
            ps.Runspace = _runSpace;
            ps.Commands.AddCommand(scriptText);

            // Run the scriptText
            var testCommand = ps.Commands.Commands[0];
            testCommand.Parameters.Add("Id", parameters[0]);
            testCommand.Parameters.Add("Version", parameters[1]);
            // Add as a test hook to pass in the provider
            testCommand.Parameters.Add("SourceRepositoryProvider", provider);
            testCommand.Parameters.Add("VsSolutionManager", new TestSolutionManager());

            // Add out-string
            ps.Commands.AddCommand("Out-String");

            // execute the script
            foreach (PSObject result in ps.Invoke())
            {
                Console.WriteLine(result.ToString());
            }

            // close the runspace
            _runSpace.Close();
        }
    }
}
