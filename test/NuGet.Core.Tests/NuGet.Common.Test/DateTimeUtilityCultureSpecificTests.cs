// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Xunit;

namespace NuGet.Common.Test
{
    [Collection(LocalizedTestCollection.TestName)]
    public class DatetimeUtilityCultureSpecificTests
    {
        [Fact]
        public void ToReadableTimeFormat_NumberFormat_LocaleSensitive_Suceeds()
        {
            try
            {
                // Prepare
                CultureUtility.SetCulture(new CultureInfo("es-ES"));

                // Act
                string actual = DatetimeUtility.ToReadableTimeFormat(TimeSpan.FromSeconds(1.23d));

                // Assert
                Assert.StartsWith("1,23", actual, StringComparison.InvariantCulture);
            }
            finally
            {
                LocalizedTestCollection.Reset();
            }
        }
    }
}
