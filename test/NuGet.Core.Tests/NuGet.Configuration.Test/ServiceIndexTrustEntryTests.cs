// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using FluentAssertions;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class ServiceIndexTrustEntryTests
    {
        [Fact]
        public void EqualsReturnsTrueForSameObject()
        {
            var value = "SERVICE_INDEX";
            var entry = new ServiceIndexTrustEntry(value);

            entry.Equals(entry).Should().BeTrue();
        }

        [Fact]
        public void EqualsReturnsTrueForIndenticalObjects()
        {
            var value = "SERVICE_INDEX";       
            var entry1 = new ServiceIndexTrustEntry(value);
            var entry2 = new ServiceIndexTrustEntry(value);

            entry1.Equals(entry2).Should().BeTrue();
        }

        [Fact]
        public void EqualsReturnsTrueForEquivalentObjects()
        {
            var value1 = "SERVICE_INDEX";
            var value2 = "service_index";
            var entry1 = new ServiceIndexTrustEntry(value1);
            var entry2 = new ServiceIndexTrustEntry(value2);

            entry1.Equals(entry2).Should().BeTrue();
        }

        [Fact]
        public void EqualsReturnsFalseForNullOtherObject()
        {
            var value = "SERVICE_INDEX";
            var entry = new ServiceIndexTrustEntry(value);

            entry.Equals(null).Should().BeFalse();
        }

        [Fact]
        public void EqualsReturnsFalseForDifferentValues()
        {
            var value1 = "SERVICE_INDEX";
            var value2 = "service_index2";
            var entry1 = new ServiceIndexTrustEntry(value1);
            var entry2 = new ServiceIndexTrustEntry(value2);

            entry1.Equals(entry2).Should().BeFalse();
        }
    }
}
