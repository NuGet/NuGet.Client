﻿using System;

namespace NuGetClient.Test.Foundation.TestAttributes.Context
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public abstract class ContextExclusionTraitAttribute : Attribute
    {
        public abstract bool ShouldExcludeContext(Context defaultContext);
    }
}
