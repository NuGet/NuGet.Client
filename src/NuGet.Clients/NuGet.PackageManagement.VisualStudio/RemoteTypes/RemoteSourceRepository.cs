// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.VisualStudio.RemoteTypes
{
    public class RemoteSourceRepository : SourceRepository
    {
        [JsonConstructor]
        public RemoteSourceRepository(PackageSource packageSource)
              : base(packageSource, Enumerable.Empty<INuGetResourceProvider>())
        {
        }

        public RemoteSourceRepository(PackageSource packageSource, IEnumerable<Lazy<INuGetResourceProvider>> providers)
            : base(packageSource, providers)
        {
        }

        public static RemoteSourceRepository Create(SourceRepository sourceRepository)
        {
            return new RemoteSourceRepository(sourceRepository.PackageSource);
        }
    }
}
