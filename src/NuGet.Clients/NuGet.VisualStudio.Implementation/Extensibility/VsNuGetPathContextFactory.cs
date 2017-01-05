// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsNuGetPathContextFactory))]
    public class VsNuGetPathContextFactory : IVsNuGetPathContextFactory
    {
        private readonly ISettings _settings;

        [ImportingConstructor]
        public VsNuGetPathContextFactory(ISettings settings)
        {
            _settings = settings;
        }

        public Task<IVsNuGetPathContext> CreateAsync(CancellationToken token)
        {
            var internalPathContext = NuGetPathContext.Create(_settings);

            var outputPathContext =  new VsNuGetPathContext(
                internalPathContext.UserPackageFolder,
                internalPathContext.FallbackPackageFolders);

            return Task.FromResult<IVsNuGetPathContext>(outputPathContext);
        }
    }
}
