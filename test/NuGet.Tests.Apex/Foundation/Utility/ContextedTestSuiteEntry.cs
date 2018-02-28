using NuGet.Tests.Foundation.TestAttributes.Context;

namespace NuGet.Tests.Foundation.Utility
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
