// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using NuGet.Protocol.Core.Types;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsPackageSourceProvider))]
    public class VsPackageSourceProvider : IVsPackageSourceProvider
    {
        private readonly Configuration.IPackageSourceProvider _packageSourceProvider;

        [ImportingConstructor]
        public VsPackageSourceProvider(ISourceRepositoryProvider sourceRepositoryProvider)
        {
            _packageSourceProvider = sourceRepositoryProvider.PackageSourceProvider;
            _packageSourceProvider.PackageSourcesChanged += PackageSourcesChanged;
        }

        public IEnumerable<KeyValuePair<string, string>> GetSources(bool includeUnOfficial, bool includeDisabled)
        {
            List<KeyValuePair<string, string>> sources = new List<KeyValuePair<string, string>>();

            foreach (var source in _packageSourceProvider.LoadPackageSources())
            {
                if ((IsOfficial(source) || includeUnOfficial)
                    && (source.IsEnabled || includeDisabled))
                {
                    // Name -> Source Uri
                    var pair = new KeyValuePair<string, string>(source.Name, source.Source);
                    sources.Add(pair);
                }
            }

            return sources;
        }

        public event EventHandler SourcesChanged;

        private void PackageSourcesChanged(object sender, EventArgs e)
        {
            if (SourcesChanged != null)
            {
                // No information is given in the event args, callers must re-request GetSources
                SourcesChanged(this, new EventArgs());
            }
        }

        private static bool IsOfficial(Configuration.PackageSource source)
        {
            bool official = source.IsOfficial;

            // override the official flag if the domain is nuget.org
            if (source.Source.StartsWith("http://www.nuget.org/", StringComparison.OrdinalIgnoreCase)
                || source.Source.StartsWith("https://www.nuget.org/", StringComparison.OrdinalIgnoreCase)
                || source.Source.StartsWith("http://api.nuget.org/", StringComparison.OrdinalIgnoreCase)
                || source.Source.StartsWith("https://api.nuget.org/", StringComparison.OrdinalIgnoreCase))
            {
                official = true;
            }

            return official;
        }
    }
}
