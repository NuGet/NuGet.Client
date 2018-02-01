using System;
using System.Reflection;
using Xunit.Sdk;

namespace NuGetClient.Test.Foundation.TestAttributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TrackingTestClassAttribute : BeforeAfterTestAttribute
    {
        public override void Before(MethodInfo methodUnderTest)
        {
            base.Before(methodUnderTest);
        }

        public override void After(MethodInfo methodUnderTest)
        {
            base.After(methodUnderTest);
        }
    }
}
