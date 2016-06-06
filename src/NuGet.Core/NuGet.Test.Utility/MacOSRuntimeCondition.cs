// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.Test.Utility
{
    public class MacOSRuntimeCondition : TestExecutionCondition
    {
        public override bool ShouldSkip => RuntimeEnvironmentHelper.IsMacOSX;

        public override string SkipReason => "Test is skipped on Mac OS X";
    }
}
