// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Client.Diagnostics
{
    public class TracingFacts
    {
        [Fact]
        public void InvocationIdsMonotonicallyIncrease()
        {
            var first = Tracing.GetNextInvocationId();
            var second = Tracing.GetNextInvocationId();
            var third = Tracing.GetNextInvocationId();

            Assert.True(third > second && second > first);
        }
    }
}
