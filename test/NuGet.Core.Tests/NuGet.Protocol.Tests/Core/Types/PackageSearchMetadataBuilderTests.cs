// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Core.Types.Tests
{
    public class PackageSearchMetadataBuilderTests
    {
        [Fact]        
        public void FromClonedPacakgeSearchMetadata_LocalPackageInfo_NotNull()
        {
            var pi = new PackageIdentity("nuget.pak.test", new NuGetVersion(0, 0, 1));
            var builder = PackageSearchMetadataBuilder.FromIdentity(pi);

            builder.Build();
        }
    }
}
