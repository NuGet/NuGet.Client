// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Test.Utility
{
    public abstract class TestExecutionCondition
    {
        public abstract bool ShouldSkip { get; }
        public abstract string SkipReason { get; }
    }
}
