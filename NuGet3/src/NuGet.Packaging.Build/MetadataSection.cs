// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Packaging.Build
{
    public class MetadataSection
    {
        private readonly List<Metadata> _entries = new List<Metadata>();

        public string Name { get; set; }

        public string ItemName { get; set; }

        public string GroupByProperty { get; set; }

        public void DefineEntry(Action<Metadata> action)
        {
            var entry = new Metadata();
            action(entry);
            _entries.Add(entry);
        }

        public IEnumerable<Metadata> GetEntries()
        {
            return _entries;
        }
    }
}
