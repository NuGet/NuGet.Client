// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;
using Xunit;

namespace NuGet.VisualStudio.Internal.Contracts.Test
{
    public sealed class FloatRangeFormatterTests : FormatterTests
    {
        private static readonly string ReleaseVersion = "a";
        private static readonly NuGetVersion MinVersion = NuGetVersion.Parse($"1.0.0-{ReleaseVersion}");

        [Theory]
        [MemberData(nameof(FloatRanges))]
        public void SerializeThenDeserialize_WithValidArguments_RoundTrips(FloatRange expectedResult)
        {
            FloatRange actualResult = SerializeThenDeserialize(FloatRangeFormatter.Instance, expectedResult);

            Assert.Equal(expectedResult, actualResult);
            Assert.Equal(expectedResult.OriginalReleasePrefix, actualResult.OriginalReleasePrefix);
        }

        public static TheoryData FloatRanges => new TheoryData<FloatRange>
            {
                { new FloatRange(NuGetVersionFloatBehavior.AbsoluteLatest) },
                { new FloatRange(NuGetVersionFloatBehavior.PrereleaseMajor, MinVersion) },
                { new FloatRange(NuGetVersionFloatBehavior.PrereleasePatch, MinVersion, ReleaseVersion) }
            };
    }
}
