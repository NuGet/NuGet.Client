// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.VisualStudio
{
    public abstract class PSAutoCompleteResource : INuGetResource
    {
        public abstract Task<IEnumerable<string>> IdStartsWith(string packageIdPrefix, bool includePrerelease, CancellationToken token);

        public abstract Task<IEnumerable<NuGetVersion>> VersionStartsWith(string packageId, string versionPrefix, bool includePrerelease, CancellationToken token);
    }
}
