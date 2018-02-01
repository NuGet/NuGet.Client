using NuGetClient.Test.Foundation.TestAttributes.Context;

namespace NuGetClient.Test.Foundation.Utility
{
    public class ContextedTestSuiteEntry
    {
        /// <summary>
        /// Name of the test.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The Context to run the test in.
        /// </summary>
        public Context Context { get; set; }
    }
}
