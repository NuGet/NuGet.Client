// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit.Sdk;
using Xunit;

namespace NuGet.Test.Utility
{
    [XunitTestCaseDiscoverer("NuGet.Test.Utility.RetryFactDiscoverer", "Test.Utility")]
    public class RetryFactAttribute : FactAttribute
    {
        public int MaxRetries { get; set; } = 0;
    }
}
