using NuGet.Tests.Foundation.TestAttributes.Context;
using Xunit.Sdk;

namespace NuGet.Tests.Foundation.TestCommands.Context
{
    public static class ContextExtensions
    {
        public static TestAttributes.Context.Context GetContext(this XunitTestCase testCase)
        {
            return (testCase as ContextBaseTestCase)?.Context;
        }
    }
}
