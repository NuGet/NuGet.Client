using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Utilities;
using NuGet.VisualStudio;

namespace NuGet.CommandLine.Test
{
    [Export(typeof(IVsPackageManagerProvider))]
    [Order(Before = "test-version1")]
    [Name("test-version0")]
    public class PackageManagerProviderTest0 : IVsPackageManagerProvider
    {
        public string PackageManagerName { get; }

        public string PackageManagerId { get; }

        public string Description { get; }

        public PackageManagerProviderTest0()
        {
            PackageManagerName = "test";
            PackageManagerId = "test-version0";
            Description = "this is a test package manager";
        }

        public async Task<bool> CheckForPackageAsync(string packageId, string projectName, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                Console.WriteLine("checking package in {0}", PackageManagerName);
                return true;
            });
        }

        public void GoToPackage(string packageId, string projectName)
        {
            Console.WriteLine("opening {0} UI", PackageManagerName);
        }
    }

    [Export(typeof(IVsPackageManagerProvider))]
    [Order(Before = "test-version2", After = "test-version0")]
    [Name("test-version1")]
    public class PackageManagerProviderTest1 : IVsPackageManagerProvider
    {
        public string PackageManagerName { get; }

        public string PackageManagerId { get; }

        public string Description { get; }

        public PackageManagerProviderTest1()
        {
            PackageManagerName = "test";
            PackageManagerId = "test-version1";
            Description = "this is a test package manager";
        }

        public async Task<bool> CheckForPackageAsync(string packageId, string projectName, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                Console.WriteLine("checking package in {0}", PackageManagerName);
                return true;
            });
        }

        public void GoToPackage(string packageId, string projectName)
        {
            Console.WriteLine("opening {0} UI", PackageManagerName);
        }
    }

    [Export(typeof(IVsPackageManagerProvider))]
    [Order(After = "test-version1")]
    [Name("test-version2")]
    public class PackageManagerProviderTest2 : IVsPackageManagerProvider
    {
        public string PackageManagerName { get; }

        public string PackageManagerId { get; }

        public string Description { get; }

        public PackageManagerProviderTest2()
        {
            PackageManagerName = "test";
            PackageManagerId = "test-version2";
            Description = "this is a test package manager";
        }

        public async Task<bool> CheckForPackageAsync(string packageId, string projectName, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                Console.WriteLine("checking package in {0}", PackageManagerName);
                return true;
            });
        }

        public void GoToPackage(string packageId, string projectName)
        {
            Console.WriteLine("opening {0} UI", PackageManagerName);
        }
    }

    [Export(typeof(IVsPackageManagerProvider))]
    [Order(Before = "test-version0")]
    [Name("test-version0Update")]
    public class PackageManagerProviderTest0Update : IVsPackageManagerProvider
    {
        public string PackageManagerName { get; }

        public string PackageManagerId { get; }

        public string Description { get; }

        public PackageManagerProviderTest0Update()
        {
            PackageManagerName = "testUpdate";
            PackageManagerId = "test-version0";
            Description = "this is a test package manager";
        }

        public async Task<bool> CheckForPackageAsync(string packageId, string projectName, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                Console.WriteLine("checking package in {0}", PackageManagerName);
                return true;
            });
        }

        public void GoToPackage(string packageId, string projectName)
        {
            Console.WriteLine("opening {0} UI", PackageManagerName);
        }
    }

}
