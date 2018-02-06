using System;

namespace NuGetClient.Test.Foundation.TestAttributes
{
    /// <summary>
    /// Use this for your class or class fixture to demand running in a consistent Application UI thread
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true)]
    public class RunOnTestUIThreadAttribute : Attribute
    {
    }
}
