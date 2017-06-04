// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using Xunit;

namespace NuGet.Build.Tasks.Test
{
    public class SourceTypeComparerTests
    {
        [Fact]
        public void SourceTypeComparer_VerifyOrder()
        {
            var sources = new[] { "a", "https://dotnet.myget.org/testfeed", "https://api.nuget.org/v3/index.json", "b", "http://testfeed", "file://C:\test", "file:///tmp/test/", "http:invalid", "/tmp/test/" };
            var expected = new[] { "https://dotnet.myget.org/testfeed", "https://api.nuget.org/v3/index.json", "http://testfeed", "file://C:\test", "file:///tmp/test/", "a", "b", "http:invalid", "/tmp/test/" };

            sources.OrderBy(s => s, new SourceTypeComparer())
                .ShouldBeEquivalentTo(expected);
        }
    }
}
