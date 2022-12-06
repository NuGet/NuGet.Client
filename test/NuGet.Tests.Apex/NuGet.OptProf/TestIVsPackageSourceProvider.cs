// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.Test.Apex.VisualStudio;
using NuGet.VisualStudio;

namespace NuGet
{
    [Export(typeof(TestIVsPackageSourceProvider))]
    public sealed class TestIVsPackageSourceProvider : VisualStudioTestService
    {
        public IEnumerable<KeyValuePair<string, string>> GetSources(bool includeUnOfficial, bool includeDisabled)
        {
            IVsPackageSourceProvider packageSourceProvider = VisualStudioObjectProviders.GetComponentModelService<IVsPackageSourceProvider>();

            return packageSourceProvider.GetSources(includeUnOfficial, includeDisabled);
        }
    }
}
