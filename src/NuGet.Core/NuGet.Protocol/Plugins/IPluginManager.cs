// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Plugins
{
    public interface IPluginManager
    {
        Task<IEnumerable<PluginCreationResult>> TryCreateAsync(
            SourceRepository source,
            CancellationToken cancellationToken);

        Task<IEnumerable<PluginDiscoveryResult>> FindAvailablePluginsAsync(CancellationToken cancellationToken);

        Task<PluginCreationResult> CreateSourceAgnosticPluginAsync(PluginDiscoveryResult result, CancellationToken cancellationToken);
    }
}
