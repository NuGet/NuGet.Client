﻿using System;
using System.Collections.Generic;
using NuGet.Configuration;

namespace NuGet.Commands.Test
{
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
}
