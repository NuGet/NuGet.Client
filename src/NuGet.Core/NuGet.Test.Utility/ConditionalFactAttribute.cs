// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Xunit;

namespace NuGet.Test.Utility
{
    public class ConditionalFactAttribute : FactAttribute
    {
        public ConditionalFactAttribute(params Type[] executionConditionTypes)
        {
            var condition = executionConditionTypes
                .Select(t => Activator.CreateInstance(t))
                .Cast<TestExecutionCondition>()
                .FirstOrDefault(c => c.ShouldSkip);

            if (condition != null)
            {
                Skip = condition.SkipReason;
            }
        }
    }
}
