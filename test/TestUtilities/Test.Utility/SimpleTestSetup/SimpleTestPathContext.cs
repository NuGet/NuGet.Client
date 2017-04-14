// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Xml.Linq;

namespace NuGet.Test.Utility
{
    /// <summary>
    /// Create a basic test layout complete with a nuget.config file containing the paths
    /// </summary>
    public class SimpleTestPathContext : IDisposable
    {
        public TestDirectory WorkingDirectory { get; }

        public string SolutionRoot { get; }

        public string UserPackagesFolder { get; }

        public string PackagesV2 { get; }

        public string NuGetConfig { get; }

        public string PackageSource { get; }

        public string FallbackFolder { get; }

        public string HttpCacheFolder { get; }

        public bool CleanUp { get; set; } = true;

        public SimpleTestPathContext()
        {
            WorkingDirectory = TestDirectory.Create();

            SolutionRoot = Path.Combine(WorkingDirectory.Path, "solution");
            UserPackagesFolder = Path.Combine(WorkingDirectory.Path, "globalPackages");
            PackagesV2 = Path.Combine(SolutionRoot, "packages");
            NuGetConfig = Path.Combine(WorkingDirectory, "NuGet.Config");
            PackageSource = Path.Combine(WorkingDirectory.Path, "source");
            FallbackFolder = Path.Combine(WorkingDirectory.Path, "fallback");
            HttpCacheFolder = Path.Combine(WorkingDirectory.Path, "v3-cache");

            Directory.CreateDirectory(SolutionRoot);
            Directory.CreateDirectory(UserPackagesFolder);
            Directory.CreateDirectory(PackageSource);
            Directory.CreateDirectory(FallbackFolder);

            CreateNuGetConfig();

            // Record who wrote this out incase a test isn't cleaning up
            File.WriteAllText(Path.Combine(WorkingDirectory, "testStack.txt"), Environment.StackTrace);
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
            globalFolder.Add(new XAttribute(XName.Get("value"), UserPackagesFolder));
            config.Add(globalFolder);

            var solutionDir = new XElement(XName.Get("add"));
            solutionDir.Add(new XAttribute(XName.Get("key"), "repositoryPath"));
            solutionDir.Add(new XAttribute(XName.Get("value"), PackagesV2));
            config.Add(solutionDir);

            var packageSources = new XElement(XName.Get("packageSources"));
            configuration.Add(packageSources);
            packageSources.Add(new XElement(XName.Get("clear")));

            var sourceEntry = new XElement(XName.Get("add"));
            sourceEntry.Add(new XAttribute(XName.Get("key"), "source"));
            sourceEntry.Add(new XAttribute(XName.Get("value"), PackageSource));
            packageSources.Add(sourceEntry);

            var disabledSources = new XElement(XName.Get("disabledPackageSources"));
            configuration.Add(disabledSources);
            disabledSources.Add(new XElement(XName.Get("clear")));

            var fallbackFolders = new XElement(XName.Get("fallbackPackageFolders"));
            configuration.Add(fallbackFolders);
            var fallbackEntry = new XElement(XName.Get("add"));
            fallbackEntry.Add(new XAttribute(XName.Get("key"), "shared"));
            fallbackEntry.Add(new XAttribute(XName.Get("value"), FallbackFolder));
            fallbackFolders.Add(fallbackEntry);

            File.WriteAllText(NuGetConfig, doc.ToString());
        }

        public void Dispose()
        {
            if (CleanUp)
            {
                WorkingDirectory.Dispose();
            }
        }
    }
}
