// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.Test.Utility
{
    /// <summary>
    /// Skip Fact if it is running on a CI machine
    /// </summary>
    /// <remarks>Unset CI environment variable or set it to CI=false to run this Fact</remarks>
    public class LocalOnlyFactAttribute
        : FactAttribute
    {
        public LocalOnlyFactAttribute()
        {
            if (XunitAttributeUtility.IsCI)
            {
                Skip = "This Fact only run on non-CI machines. To run it, set the env var CI=false";
            }
        }
    }
}
