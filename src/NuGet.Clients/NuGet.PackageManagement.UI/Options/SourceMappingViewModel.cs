// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.Options
{
    public class SourceMappingViewModel
    {
        public string ID { get; set; }
        public IReadOnlyList<PackageSourceContextInfo> Sources { get; private set; }

        public string SourcesString
        {
            get
            {
                return string.Join(", ", Sources.Select(s => s.Name));
            }
        }

        public SourceMappingViewModel(string packageId, List<PackageSourceContextInfo> packageSources)
        {
            ID = packageId;
            Sources = packageSources;
        }
    }
}
