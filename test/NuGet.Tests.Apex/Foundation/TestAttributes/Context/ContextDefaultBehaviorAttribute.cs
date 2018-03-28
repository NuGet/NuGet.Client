using System;

namespace NuGet.Tests.Foundation.TestAttributes.Context
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class ContextDefaultBehaviorAttribute : Attribute
    {
        public ContextBehavior DefaultBehavior { get; private set; }

        public ContextDefaultBehaviorAttribute(ContextBehavior defaultBehavior)
        {
            this.DefaultBehavior = defaultBehavior;
        }
    }
}
