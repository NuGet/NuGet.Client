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

namespace StandaloneUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [ImportMany]
        public Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>[] _resourceProviders;

        [Import]
        public IUserInterfaceService _uiService;

        private CompositionContainer _container;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Grid_Initialized(object sender, EventArgs e)
        {
            _container = Initialize();

            this.Title = "NuGet Standalone UI";
            Height = 800;
            Width = 1000;

            var repositoryProvider = new SourceRepositoryProvider(new V3OnlyPackageSourceProvider(), _resourceProviders);

            NuGetProject project = new FolderNuGetProject("tmpproject");

            PackageManagerModel model = new PackageManagerModel(repositoryProvider, project);
            PackageManagerControl control = new PackageManagerControl(model, _uiService);

            layoutGrid.Children.Add(control);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        private CompositionContainer Initialize()
        {
            string assemblyName = Assembly.GetEntryAssembly().FullName;

            var path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            using (var catalog = new AggregateCatalog(new AssemblyCatalog(Assembly.GetExecutingAssembly().Location),
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
            return new List<NuGet.Configuration.PackageSource>() { new NuGet.Configuration.PackageSource("https://az320820.vo.msecnd.net/ver3-preview/index.json", "v3") };
        }

        public event EventHandler PackageSourcesSaved;

        public void SavePackageSources(IEnumerable<NuGet.Configuration.PackageSource> sources)
        {
            throw new NotImplementedException();
        }
    }
}
