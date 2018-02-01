using System;

namespace NuGetClient.Test.Foundation.TestAttributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true)]
    public class RunOnSTAThreadAttribute : Attribute
    {
    }
}
