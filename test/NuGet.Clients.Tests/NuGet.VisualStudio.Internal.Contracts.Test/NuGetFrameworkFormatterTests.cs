// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;
using Xunit;

namespace NuGet.VisualStudio.Internal.Contracts.Test
{
    public sealed class NuGetFrameworkFormatterTests : FormatterTests
    {
        [Theory]
        [MemberData(nameof(NuGetFrameworks))]
        public void SerializeThenDeserialize_WithValidArguments_RoundTrips(NuGetFramework expectedResult)
        {
            NuGetFramework actualResult = SerializeThenDeserialize(NuGetFrameworkFormatter.Instance, expectedResult);

            Assert.Equal(expectedResult, actualResult);
        }

        public static TheoryData NuGetFrameworks => new TheoryData<NuGetFramework>
            {
                { NuGetFramework.Parse("net50") }
            };
    }
}
