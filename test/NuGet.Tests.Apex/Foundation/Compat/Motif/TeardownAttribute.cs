using System;

namespace NuGet.Tests.Foundation.Compat.Motif
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class TeardownAttribute : Attribute
    {
        public TeardownAttribute() { }
    }
}
