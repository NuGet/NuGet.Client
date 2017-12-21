// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet.Common;
using NuGet.Repositories;
using NuGet.RuntimeModel;

namespace NuGet.Commands
{
    public class RestoreCommandCache
    {
        private readonly ConcurrentDictionary<string, RuntimeGraph> _runtimeGraphs = new ConcurrentDictionary<string, RuntimeGraph>(PathUtility.GetStringComparerBasedOnOS());

        public RuntimeGraph GetRuntimeGraph(IEnumerable<LocalPackageInfo> packages)
        {
            var sorted = packages
                .Select(e => Tuple.Create(e.ExpandedPath, e.RuntimeGraph, e))
                .Where(e => e.Item2 != null)
                .OrderBy(e => e.Item1, PathUtility.GetStringComparerBasedOnOS())
                .ToList();

            if (sorted.Count == 0)
            {
                return RuntimeGraph.Empty;
            }
            else
            {
                var key = string.Join("|", sorted.Select(e => e.Item1));

                return _runtimeGraphs.GetOrAdd(key, k => RuntimeGraph.Merge(sorted.Select(e => e.Item2)));
            }
        }
    }
}
