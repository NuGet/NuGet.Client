using NuGet.Client;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Reflection;
using System.Windows;
using System.Linq;
using NuGet.ProjectManagement;
using NuGet.Frameworks;
using System.Diagnostics;
using Test.Utility;

namespace StandaloneUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [Import]
        public INuGetUIFactory _uiServiceFactory;

        [ImportMany]
        public IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> _resourceProviders;

        [Import]
        public ISettings _settings;

        // !!! [Import]
        public ISourceControlManagerProvider _sourceControlManagerProvider;

        private CompositionContainer _container;
        private PackageManagerControl _packageManagerControl;

        public MainWindow()
        {
            InitializeComponent();
            CreatePackageManagerControl();
        }

        private void CreatePackageManagerControl()
        {
            _container = Initialize();

            this.Title = "NuGet Standalone UI";
            Height = 800;
            Width = 1000;

            var repositoryProvider = new SourceRepositoryProvider(_resourceProviders, _settings);
            var settings = new DefaultSettings();

            var testSolutionManager = new TestSolutionManager(@"c:\temp\test");
            
            var projectA = testSolutionManager.AddNewMSBuildProject("projectA");
            var projectB = testSolutionManager.AddNewMSBuildProject("projectB");
            //var projectC = testSolutionManager.AddProjectKProject("projectK");

            var projects = new NuGetProject[] { projectA, projectB };            

            var packageRestoreManager = new PackageRestoreManager(repositoryProvider, settings, testSolutionManager);
            var contextFactory = new StandaloneUIContextFactory(
                repositoryProvider, 
                testSolutionManager, 
                settings, 
                packageRestoreManager: packageRestoreManager,
                optionsPage: null);
            var context = contextFactory.Create(@"c:\temp\test\settings.txt", projects);
            var uiController = _uiServiceFactory.Create(
                context,
                new NuGetUIProjectContext(new StandaloneUILogger(_textBox, _scrollViewer), _sourceControlManagerProvider));

            PackageManagerModel model = new PackageManagerModel(uiController, context);
            model.SolutionName = "test solution";
            _packageManagerControl = new PackageManagerControl(model);
            layoutGrid.Children.Add(_packageManagerControl);
        }

        private CompositionContainer Initialize()
        {
            string assemblyName = Assembly.GetEntryAssembly().FullName;

            var path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            using (var catalog = new AggregateCatalog(
                new AssemblyCatalog(Assembly.Load(assemblyName)),
                new DirectoryCatalog(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "*.dll")))
            {
                var container = new CompositionContainer(catalog);

                try
                {
                    container.ComposeParts(this);
                    return container;
                }
                catch (Exception ex)
                {
                    Debug.Fail("MEF: " + ex.ToString());

                    throw;
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _packageManagerControl.Model.Context.SaveSettings();
        }
    }

    internal class V3OnlyPackageSourceProvider : IPackageSourceProvider
    {
        public void DisablePackageSource(NuGet.Configuration.PackageSource source)
        {
            throw new NotImplementedException();
        }

        public bool IsPackageSourceEnabled(NuGet.Configuration.PackageSource source)
        {
            return true;
        }

        public IEnumerable<NuGet.Configuration.PackageSource> LoadPackageSources()
        {
            return new List<NuGet.Configuration.PackageSource>() { new NuGet.Configuration.PackageSource("https://api.nuget.org/v3/index.json", "nuget.org v3") };
        }

        public event EventHandler PackageSourcesSaved;

        public void SavePackageSources(IEnumerable<NuGet.Configuration.PackageSource> sources)
        {
            throw new NotImplementedException();
        }

        public string ActivePackageSourceName
        {
            get { throw new NotImplementedException(); }
        }

        public void SaveActivePackageSource(PackageSource source)
        {
            throw new NotImplementedException();
        }
    }
}
