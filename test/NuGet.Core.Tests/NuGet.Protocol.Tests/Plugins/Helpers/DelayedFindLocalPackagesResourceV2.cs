// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using NuGet.Common;

namespace NuGet.Protocol.Tests.Plugins.Helpers
{
    // Used to simulate slow local package folder read or over the network UNC drive read
    internal class DelayedFindLocalPackagesResourceV2 : FindLocalPackagesResourceV2
    {
        private readonly int? _delay;

        public DelayedFindLocalPackagesResourceV2(string root, int? delay)
            : base(root)
        {
            _delay = delay;
        }

        public DelayedFindLocalPackagesResourceV2(string root)
            : this(root: root, delay: null)
        { }

        public override IEnumerable<LocalPackageInfo> GetPackages(ILogger logger, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var packages = LocalFolderUtility.GetPackagesV2(Root, logger, token);

            if(_delay.HasValue)
            {
                // intentional delay
                Thread.Sleep(_delay.Value);
                token.ThrowIfCancellationRequested();
            }

            // Filter out any duplicates that may appear in the folder multiple times.
            return LocalFolderUtility.GetDistinctPackages(packages);
        }
    }
}
