using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
using NuGetConsole;
using Test.Utility;

namespace StandaloneConsole
{
    internal class ServiceProvider : IServiceProvider
    {
        private readonly CompositionContainer _container;

        // The PowerShell host provider name is defined in PowerShellHostProvider.cs
        const string PowerShellHostProviderName = "NuGetConsole.Host.PowerShell";

        [Import]
        public IHostProvider HostProvider { get; private set; }

        [Import]
        public ICommandExpansionProvider CommandExpansionProvider { get; private set; }

        public ServiceProvider()
        {
            _container = Initialize();
        }

        public object GetService(Type serviceType)
        {
            if (string.Equals("SDTE", serviceType?.Name))
            {
                return null;
            }

            if (string.Equals("SComponentModel", serviceType?.Name))
            {
                return null;
            }

            var def = new ContractBasedImportDefinition(
                serviceType.FullName, null, null, ImportCardinality.ZeroOrOne, true, false, CreationPolicy.Any);
            return _container.GetExports(def).FirstOrDefault()?.Value;
        }

        private CompositionContainer Initialize()
        {
            var assemblyName = Assembly.GetEntryAssembly().FullName;

            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var catalog = new AggregateCatalog(
                new AssemblyCatalog(Assembly.Load(assemblyName)),
                new DirectoryCatalog(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "NuGet*.dll"));

            var container = new CompositionContainer(catalog);

            var settings = Settings.LoadDefaultSettings(null, null, null);
            container.ComposeExportedValue(settings);

            var sourceRepositoryProvider = new SourceRepositoryProvider(settings, Repository.Provider.GetVisualStudio());
            container.ComposeExportedValue<ISourceRepositoryProvider>(sourceRepositoryProvider);

            var testSolutionManager = new TestSolutionManager(@"c:\temp\test");
            var projectA = testSolutionManager.AddNewMSBuildProject("projectA");
            container.ComposeExportedValue<ISolutionManager>(testSolutionManager);

            var sourceControlManager = new TestSourceControlManager();
            var sourceControlManagerProvider = new TestSourceControlManagerProvider(sourceControlManager);
            container.ComposeExportedValue<ISourceControlManagerProvider>(sourceControlManagerProvider);

            container.ComposeParts(this);
            return container;
        }
    }

    internal class ConsoleApplication : System.Windows.Application
    {
        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainThread = Thread.CurrentThread;
            var synchronizationContext = SynchronizationContext.Current;
            NuGetUIThreadHelper.SetCustomJoinableTaskFactory(mainThread, synchronizationContext);

            var sp = new ServiceProvider();
            ServiceLocator.InitializePackageServiceProvider(sp);

            var host = sp.HostProvider?.CreateHost(async: false);
            host.ActivePackageSource = "All";
            var commandExpansion = sp.CommandExpansionProvider?.Create(host);

            var console = new StandaloneConsole();
            host.Initialize(console);

            Task.Run(async () =>
            {
                var listener = new ConsoleListener(host, commandExpansion);
                await listener.RunAsync(console).ConfigureAwait(false);
            });
        }
    }

    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var app = new ConsoleApplication();
            app.Run();
        }
    }
}
