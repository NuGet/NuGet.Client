// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using FluentAssertions;
using NuGet.Build.Tasks.Console;
using NuGet.Commands;
using Xunit;

namespace NuGet.Build.Tasks.Test
{
    public class ProjectItemInstanceEvaluatedIncludeComparerTests
    {
        [Fact]
        public void ProjectItemInstanceEvaluatedIncludeComparer_Equals_DeDupesWhenDifferentCasing()
        {
            ProjectItemInstanceEvaluatedIncludeComparer.Instance
                .Equals(
                    new MockMSBuildProjectItem("one", new Dictionary<string, string>()),
                    new MockMSBuildProjectItem("oNE", new Dictionary<string, string>()))
                .Should()
                .BeTrue();
        }

        [Fact]
        public void ProjectItemInstanceEvaluatedIncludeComparer_GetHashCode_DeDupesWhenDifferentCasing()
        {
            Action act = () =>
            {
                var items = new HashSet<IMSBuildProjectItem>(ProjectItemInstanceEvaluatedIncludeComparer.Instance);

                items.Add(new MockMSBuildProjectItem("one", new Dictionary<string, string>()));
                items.Add(new MockMSBuildProjectItem("oNe", new Dictionary<string, string>()));
                items.Add(new MockMSBuildProjectItem("ONE", new Dictionary<string, string>()));

                items.Count.Should().Be(1);
            };

            act.ShouldNotThrow();
        }
    }
}
