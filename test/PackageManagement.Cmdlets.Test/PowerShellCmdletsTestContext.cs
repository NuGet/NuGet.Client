using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Management.Automation.Runspaces;
using NuGet.Protocol.Core.Types;

namespace PackageManagement.Cmdlets.Test
{
    public class PowerShellCmdletsTestContext : IDisposable
    {
        [ImportMany]
        public IEnumerable<Lazy<INuGetResourceProvider>> ResourceProviders { get; set; }

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
