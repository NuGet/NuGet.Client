namespace NuGetClient.Test.Foundation.Traits
{
    // We put the traits in a static class in the Tests.Foundation root to get consistency and discoverability
    public static partial class Traits
    {
        /// <summary>
        /// Test classification
        /// </summary>
        public enum TestType
        {
            Sample,
            Unit,
            Component,
            Scenario,
            SetupTeardown,
            DevRollingTest
        }
    }
}
