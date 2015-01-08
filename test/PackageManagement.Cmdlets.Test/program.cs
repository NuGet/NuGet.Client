using NuGet.Client;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.PowerShellCmdlets;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PackageManagement.Cmdlets.Test
{
    public class Program
    {
        [ImportMany]
        public IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> ResourceProviders { get; set; }

        static void Main(string[] args)
        {
            var p = new Program();
            p.InitializeComponents();
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
                ISettings settings = Settings.LoadDefaultSettings(Environment.ExpandEnvironmentVariables("%systemdrive%"), null, null);
                var packageSourceProvider = new PackageSourceProvider(settings);
                var packageSources = packageSourceProvider.LoadPackageSources();
                SourceRepositoryProvider provider = new SourceRepositoryProvider(packageSourceProvider, ResourceProviders);
                InstallPackageCommand installCommand = new InstallPackageCommand();
                Console.WriteLine(provider.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
