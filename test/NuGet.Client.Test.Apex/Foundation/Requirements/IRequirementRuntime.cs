﻿using NuGetClient.Test.Foundation.TestAttributes.Context;

namespace NuGetClient.Test.Foundation.Requirements
{
    /// <summary>
    /// Determines if a specific Requirement is met within the constraints of a
    /// specific Context and runtime environment.
    /// </summary>
    public interface IRequirementRuntime
    {
        Requirement Requirement { get; }

        bool SatisfiedInContext(Context context);
    }
}
