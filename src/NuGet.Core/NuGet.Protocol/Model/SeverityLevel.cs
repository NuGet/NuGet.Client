// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Protocol
{
    /// <summary>
    /// Represents severity levels implemented in nuget.org protocol
    /// </summary>
    /// <see href="https://docs.microsoft.com/nuget/api/registration-base-url-resource#vulnerabilities" />
    public enum SeverityLevel
    {
        None = -1,
        Low = 0,
        Moderate = 1,
        High = 2,
        Critical = 3,
    }

    public static class SeverityLevelExtensions
    {
        public static SeverityLevel FromValue(int value)
        {
            if (Enum.IsDefined(typeof(SeverityLevel), value))
            {
                return (SeverityLevel)value;
            }

            return SeverityLevel.None;
        }
    }

}
