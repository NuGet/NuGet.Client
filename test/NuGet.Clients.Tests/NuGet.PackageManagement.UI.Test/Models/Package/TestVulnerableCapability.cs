// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI.Test.Models.Package
{
    internal class TestVulnerableCapability : VulnerableCapability
    {
        public TestVulnerableCapability(IReadOnlyList<PackageVulnerabilityMetadataContextInfo> vulnerabilities)
        {
            Vulnerabilities = vulnerabilities;
        }

        public override Task PopulateDataAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
