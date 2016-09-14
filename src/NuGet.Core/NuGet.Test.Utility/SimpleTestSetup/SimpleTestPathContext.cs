using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGet.Test.Utility
{
    /// <summary>
    /// Create a basic test layout complete with a nuget.config file containing the paths
    /// </summary>
    public class SimpleTestPathContext : IDisposable
    {
        public TestDirectory WorkingDirectory { get; }

        public DirectoryInfo SolutionRoot { get; }

        public DirectoryInfo UserPackagesFolder { get; }

        public DirectoryInfo PackagesV2 { get; }

        public FileInfo NuGetConfig { get; }

        public DirectoryInfo PackageSource { get; }

        public DirectoryInfo FallbackFolder { get; }

        public SimpleTestPathContext()
        {
            WorkingDirectory = TestFileSystemUtility.CreateRandomTestFolder();

            SolutionRoot = new DirectoryInfo(Path.Combine(WorkingDirectory.Path, "solution"));
            UserPackagesFolder = new DirectoryInfo(Path.Combine(WorkingDirectory.Path, "globalPackages"));
            PackagesV2 = new DirectoryInfo(Path.Combine(SolutionRoot.FullName, "packages"));
            NuGetConfig = new FileInfo(Path.Combine(SolutionRoot.FullName, "NuGet.Config"));
            PackageSource = new DirectoryInfo(Path.Combine(WorkingDirectory.Path, "source"));
            FallbackFolder = new DirectoryInfo(Path.Combine(WorkingDirectory.Path, "fallback"));

            SolutionRoot.Create();
            UserPackagesFolder.Create();
            PackageSource.Create();
            FallbackFolder.Create();
        }

        private void CreateNuGetConfig()
        {
            var doc = new XDocument();
            var configuration = new XElement(XName.Get("configuration"));
            doc.Add(configuration);

            var config = new XElement(XName.Get("config"));
            configuration.Add(config);

            var globalFolder = new XElement(XName.Get("add"));
            globalFolder.Add(new XAttribute(XName.Get("key"), "globalPackagesFolder"));
            globalFolder.Add(new XAttribute(XName.Get("value"), UserPackagesFolder.FullName));
            config.Add(globalFolder);

            var solutionDir = new XElement(XName.Get("add"));
            solutionDir.Add(new XAttribute(XName.Get("key"), "repositoryPath"));
            solutionDir.Add(new XAttribute(XName.Get("value"), PackagesV2.FullName));
            config.Add(solutionDir);

            var packageSources = new XElement(XName.Get("packageSources"));
            configuration.Add(packageSources);
            packageSources.Add(new XElement(XName.Get("clear")));

            var sourceEntry = new XElement(XName.Get("add"));
            sourceEntry.Add(new XAttribute(XName.Get("key"), "source"));
            sourceEntry.Add(new XAttribute(XName.Get("value"), PackageSource.FullName));
            packageSources.Add(sourceEntry);

            var disabledSources = new XElement(XName.Get("disabledPackageSources"));
            configuration.Add(disabledSources);
            disabledSources.Add(new XElement(XName.Get("clear")));

            var fallbackFolders = new XElement(XName.Get("fallbackPackageFolders"));
            configuration.Add(fallbackFolders);
            var fallbackEntry = new XElement(XName.Get("add"));
            sourceEntry.Add(new XAttribute(XName.Get("key"), "shared"));
            sourceEntry.Add(new XAttribute(XName.Get("value"), FallbackFolder.FullName));
            fallbackFolders.Add(fallbackEntry);

            File.WriteAllText(NuGetConfig.FullName, doc.ToString());
        }

        public void Dispose()
        {
            WorkingDirectory.Dispose();
        }
    }
}
