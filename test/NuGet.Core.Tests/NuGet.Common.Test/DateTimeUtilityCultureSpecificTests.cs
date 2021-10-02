// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Common.Test
{
    public class DatetimeUtilityCultureSpecificTests
    {
        public DatetimeUtilityCultureSpecificTests()
        {
            CultureUtility.SetCulture(new System.Globalization.CultureInfo("es-ES"));
        }

        [Fact]
        public void ToReadableTimeFormat_NumberFormat_LocaleSensitive_Suceeds()
        {
            var actual = DatetimeUtility.ToReadableTimeFormat(TimeSpan.FromSeconds(1.23d));

            Assert.StartsWith("1,23", actual, StringComparison.InvariantCulture);
        }
    }
}
