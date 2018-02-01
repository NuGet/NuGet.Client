using System;

namespace NuGetClient.Test.Foundation.TestAttributes.Context
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ContextDefaultStateAttribute : Attribute
    {
        public Context Context { get; private set; }

        public ContextDefaultStateAttribute(Context defaultContext)
        {
            this.Context = defaultContext;
        }
    }
}
