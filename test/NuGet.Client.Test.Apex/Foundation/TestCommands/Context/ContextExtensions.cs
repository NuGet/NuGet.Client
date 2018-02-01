using NuGetClient.Test.Foundation.TestAttributes.Context;
using Xunit.Sdk;

namespace NuGetClient.Test.Foundation.TestCommands.Context
{
    public static class ContextExtensions
    {
        public static TestAttributes.Context.Context GetContext(this XunitTestCase testCase)
        {
            return (testCase as ContextBaseTestCase)?.Context;
        }
    }
}
