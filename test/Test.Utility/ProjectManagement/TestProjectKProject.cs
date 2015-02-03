using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem.Interop;
using NuGet.ProjectManagement.Projects;

namespace Test.Utility.ProjectManagement
{
    public class NuGetPackageMoniker : INuGetPackageMoniker
    {
        public string Id
        {
            get;
            set;
        }

        public string Version
        {
            get;
            set;
        }
    }
    public class TestProjectKProject : INuGetPackageManager
    {
        private List<NuGetPackageMoniker> _installedPackages;

        public TestProjectKProject()
        {
            _installedPackages = new List<NuGetPackageMoniker>();
        }

        public bool CanSupport(string optionName, NuGetOperation operation)
        {
            return true;
        }

        public Task<IReadOnlyCollection<object>> GetInstalledPackagesAsync(System.Threading.CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                IReadOnlyCollection<object> result = _installedPackages.Cast<object>().ToList();
                return result;
            });
        }

        public Task<IReadOnlyCollection<FrameworkName>> GetSupportedFrameworksAsync(System.Threading.CancellationToken cancellationToken)
        {
            return Task.Run(() =>
                {
                    var frameworks = new List<FrameworkName>();
                    IReadOnlyCollection<FrameworkName> result = frameworks;
                    return result;
                });
        }

        public Task InstallPackageAsync(INuGetPackageMoniker package, IReadOnlyDictionary<string, object> options, System.IO.TextWriter logger, IProgress<INuGetPackageInstallProgress> progress, System.Threading.CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                _installedPackages.Add(new NuGetPackageMoniker()
                {
                    Id = package.Id,
                    Version = package.Version
                });
            });
        }

        public Task UninstallPackageAsync(INuGetPackageMoniker package, IReadOnlyDictionary<string, object> options, System.IO.TextWriter logger, IProgress<INuGetPackageInstallProgress> progress, System.Threading.CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                _installedPackages.RemoveAll(p => p.Id == package.Id && p.Version == package.Version);
            });
        }
    }
}
