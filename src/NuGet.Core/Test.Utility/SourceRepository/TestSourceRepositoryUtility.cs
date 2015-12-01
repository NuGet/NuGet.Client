// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;

namespace Test.Utility
{
    public class TestSourceRepositoryUtility
    {
        public static PackageSource V2PackageSource = new PackageSource("https://www.nuget.org/api/v2/", "v2");
        public static PackageSource V3PackageSource = new PackageSource("https://api.nuget.org/v3/index.json", "v3");

        public IEnumerable<Lazy<INuGetResourceProvider>> ResourceProviders { get; private set; }

        private void Initialize()
        {
            ResourceProviders = Repository.Provider.GetVisualStudio();
            //var aggregateCatalog = new AggregateCatalog();
            //{
            //    aggregateCatalog.Catalogs.Add(new DirectoryCatalog(Environment.CurrentDirectory, "*.dll"));
            //    var container = new CompositionContainer(aggregateCatalog);
            //    container.ComposeParts(this);
            //    return container;
            //}
        }

        public static SourceRepositoryProvider CreateV3OnlySourceRepositoryProvider()
        {
            return CreateSourceRepositoryProvider(new List<PackageSource> { V3PackageSource });
        }

        public static SourceRepositoryProvider CreateV2OnlySourceRepositoryProvider()
        {
            return CreateSourceRepositoryProvider(new List<PackageSource> { V2PackageSource });
        }

        public static SourceRepositoryProvider CreateSourceRepositoryProvider(IEnumerable<PackageSource> packageSources)
        {
            var thisUtility = new TestSourceRepositoryUtility();
            //var container = thisUtility.Initialize();
            thisUtility.Initialize();
            var packageSourceProvider = new TestPackageSourceProvider(packageSources);

            var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, thisUtility.ResourceProviders);
            return sourceRepositoryProvider;
        }

        public static SourceRepositoryProvider CreateSourceRepositoryProvider(IPackageSourceProvider packageSourceProvider)
        {
            var thisUtility = new TestSourceRepositoryUtility();
            //var container = thisUtility.Initialize();
            thisUtility.Initialize();

            var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, thisUtility.ResourceProviders);
            return sourceRepositoryProvider;
        }
    }

    /// <summary>
    /// Provider that only returns V3 as a source
    /// </summary>
    public class TestPackageSourceProvider : IPackageSourceProvider
    {
        private IEnumerable<PackageSource> PackageSources { get; set; }

        public TestPackageSourceProvider(IEnumerable<PackageSource> packageSources)
        {
            PackageSources = packageSources;
        }

        public void DisablePackageSource(PackageSource source)
        {
            source.IsEnabled = false;
        }

        public bool IsPackageSourceEnabled(PackageSource source)
        {
            return true;
        }

        public IEnumerable<PackageSource> LoadPackageSources()
        {
            return PackageSources;
        }

        public event EventHandler PackageSourcesChanged;

        public void SavePackageSources(IEnumerable<PackageSource> sources)
        {
            PackageSources = sources;
            if (PackageSourcesChanged != null)
            {
                PackageSourcesChanged(this, null);
            }
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

    public static class TestPackageSourceSettings
    {
        public static string TempPackageSourceContents =
            @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <packageSources>
    <add key='nuget.org' value='https://www.nuget.org/api/v2/' />
  </packageSources>
</configuration>";

        public static string CreateAndGetSettingFilePath()
        {
            var tempFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var fileName = "nuget.config";

            File.WriteAllText(Path.Combine(tempFolder, fileName), TempPackageSourceContents);
            return tempFolder;
        }
    }
}
