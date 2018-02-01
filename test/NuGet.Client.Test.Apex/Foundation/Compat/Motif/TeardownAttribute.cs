using System;

namespace NuGetClient.Test.Foundation.Compat.Motif
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class TeardownAttribute : Attribute
    {
        public TeardownAttribute() { }
    }
}
