// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using Xunit;

namespace NuGet.Test.Utility
{
    /// <summary>
    /// Skip the test if not on a CI.
    /// </summary>
    public class CIOnlyTheoryAttribute
        : TheoryAttribute
    {
        public CIOnlyTheoryAttribute()
        {
            if (!XunitAttributeUtility.IsCI)
            {
                Skip = "This test only runs on the CI. To run it locally set the env var CI=true";
            }
        }
    }
}
