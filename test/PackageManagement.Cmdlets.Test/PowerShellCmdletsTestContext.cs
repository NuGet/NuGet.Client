using NuGet.Client;
using NuGet.Configuration;
using NuGet.PackageManagement;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace PackageManagement.Cmdlets.Test
{
    public class PowerShellCmdletsTestContext : IDisposable
    {
        [ImportMany]
        public IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> ResourceProviders { get; set; }

        public Runspace RunSpace {get; set;}

        public PowerShellCmdletsTestContext()
        {
            InitializeComponents();
            ImportModuleAndInitializeRunSpace();
        }

        public void Dispose()
        {
            RunSpace.Close();
        }

        private void InitializeComponents()
        {
            var aggregateCatalog = new AggregateCatalog();
            {
                aggregateCatalog.Catalogs.Add(new DirectoryCatalog(Environment.CurrentDirectory, "*.dll"));
                var container = new CompositionContainer(aggregateCatalog);
                container.ComposeParts(this);
            }
        }

        private void ImportModuleAndInitializeRunSpace()
        {
            InitialSessionState initial = InitialSessionState.CreateDefault();
            string assemblyPath = Path.Combine(Environment.CurrentDirectory, @"..\..\..\..\src\PackageManagement.PowerShellCmdlets\bin\debug\NuGet.PackageManagement.PowerShellCmdlets.dll");
            initial.ImportPSModule(new string[] { assemblyPath });
            RunSpace = RunspaceFactory.CreateRunspace(initial);
            RunSpace.Open();
        }
    }
}
