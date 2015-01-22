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

        //[Import]
        public INuGetUIContextFactory _contextFactory;

        private CompositionContainer _container;

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
            var projects = new NuGetProject[] { projectA, projectB };            

            var packageRestoreManager = new PackageRestoreManager(repositoryProvider, settings, testSolutionManager);
            _contextFactory = new NuGetUIContextFactory(repositoryProvider, new MySolutionManager(), 
                settings, 
                packageRestoreManager: packageRestoreManager,
                optionsPage: null);
            var context = _contextFactory.Create(projects);
            var uiController = new StandaloneNuGetUI(
                _uiServiceFactory.Create(
                    projects,
                    context,
                    new NuGetUIProjectContext(new StandaloneUILogger(_textBox, _scrollViewer))));

            PackageManagerModel model = new PackageManagerModel(uiController, context);
            model.SolutionName = "test solution";
            PackageManagerControl control = new PackageManagerControl(model);
            layoutGrid.Children.Add(control);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

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
    }
}
