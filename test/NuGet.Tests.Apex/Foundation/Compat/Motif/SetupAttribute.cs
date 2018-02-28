using System;

namespace NuGet.Tests.Foundation.Compat.Motif
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class SetupAttribute : Attribute
    {
        public SetupAttribute() { }
    }
}
