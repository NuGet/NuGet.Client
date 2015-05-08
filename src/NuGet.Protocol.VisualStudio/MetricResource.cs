// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.VisualStudio
{
    public abstract class MetricResource : INuGetResource
    {
        public abstract Task RecordMetric(IDictionary<string, string> metadata, CancellationToken token);
    }
}
