using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
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

        [Import]
        public ISettings _settings;

        // !!! [Import]
        public ISourceControlManagerProvider _sourceControlManagerProvider;
        public ICommonOperations _commonOperations;

        private CompositionContainer _container;
        private PackageManagerControl _packageManagerControl;

        public MainWindow()
        {
            InitializeComponent();
            _commonOperations = new StandAloneUICommonOperations();
            CreatePackageManagerControl();
        }

        private void CreatePackageManagerControl()
        {
            _container = Initialize();

            this.Title = "NuGet Standalone UI";
            Height = 800;
            Width = 1000;

            var repositoryProvider = new SourceRepositoryProvider(_settings, Repository.Provider.GetVisualStudio());
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
                new NuGetUIProjectContext(new StandaloneUILogger(_textBox, _scrollViewer), _sourceControlManagerProvider, _commonOperations));

            PackageManagerModel model = new PackageManagerModel(uiController, context);
            model.SolutionName = "test solution";
            _packageManagerControl = new PackageManagerControl(model, _settings);
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
            _packageManagerControl.SaveSettings();
            _packageManagerControl.Model.Context.PersistSettings();
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

#pragma warning disable 0067
        public event EventHandler PackageSourcesChanged;
#pragma warning restore 0067

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

    internal class StandAloneUICommonOperations : ICommonOperations
    {
        public System.Threading.Tasks.Task OpenFile(string fullPath)
        {
            try
            {
                Process.Start(@"C:\windows\system32\notepad.exe", fullPath);
            }
            catch (Exception)
            {
            }
            return System.Threading.Tasks.Task.FromResult(0);
        }
    }
}
