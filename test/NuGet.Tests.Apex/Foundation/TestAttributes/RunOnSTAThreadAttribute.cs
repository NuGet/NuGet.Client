using System;

namespace NuGet.Tests.Foundation.TestAttributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true)]
    public class RunOnSTAThreadAttribute : Attribute
    {
    }
}
