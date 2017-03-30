using System;

namespace NuGet.Protocol.Core.Types
{
    public static class NuGetTestMode
    {
        private const string _testModeEnvironmentVariableName = "NuGetTestModeEnabled";
        public const string NuGetTestClientName = "NuGet Test Client";

        static NuGetTestMode()
        {
            // cached for the life-time of the app domain
            Enabled = FromEnvironmentVariable();
        }

        public static bool Enabled { get; private set; }

        private static bool FromEnvironmentVariable()
        {
            var testMode = Environment.GetEnvironmentVariable(_testModeEnvironmentVariableName);
            if (String.IsNullOrEmpty(testMode))
            {
                return false;
            }

            bool isEnabled;
            return Boolean.TryParse(testMode, out isEnabled) && isEnabled;
        }


        /// <summary>
        /// Intended for internal use only: utility method for testing purposes.
        /// </summary>
        public static T InvokeTestFunctionAgainstTestMode<T>(Func<T> function, bool testModeEnabled)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }

            var valueBeforeTestRun = Enabled;

            Enabled = testModeEnabled;

            var result = function();

            Enabled = valueBeforeTestRun;

            return result;
        }
    }
}