// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;

namespace NuGet.Common.Test
{
    [Collection(LocalizedTestCollection.TestName)]
    public class DateTimeUtilityCultureInvariantTests
    {
        public static IEnumerable<object[]> GetData()
        {
            return new[]
            {
                new object[] { "1.23 sec", TimeSpan.FromSeconds(1.23d) },
                new object[] { "0 ms", TimeSpan.FromMilliseconds(0.01d) }, // round down
                new object[] { "1 ms", TimeSpan.FromMilliseconds(0.56d) }, // round up
                new object[] { "1 ms", TimeSpan.FromMilliseconds(1.21d) }, // round down
                new object[] { "92183.91 hr", TimeSpan.FromHours(92183.91d) },
                new object[] { "1 hr", TimeSpan.FromSeconds(3600.0d) },
                new object[] { "3.6 min", TimeSpan.FromMinutes(3.6d) },
                new object[] { "72 hr", TimeSpan.FromDays(3.0d) },
            };
        }

        [Theory]
        [MemberData(nameof(GetData))]
        public void ToReadableTimeFormat_CultureInvariant_Succeeds(string expected, TimeSpan time)
        {
            // Arrange
            try
            {
                // Act
                CultureUtility.SetCulture(CultureInfo.InvariantCulture);
                var actual = DatetimeUtility.ToReadableTimeFormat(time);

                // Assert
                Assert.Equal(expected, actual);
            }
            finally
            {
                LocalizedTestCollection.Reset();
            }
        }
    }
}
