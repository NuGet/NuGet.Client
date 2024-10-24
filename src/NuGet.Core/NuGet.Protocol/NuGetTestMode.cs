// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

namespace NuGet.Protocol.Core.Types
{
    public static class NuGetTestMode
    {
        private const string _testModeEnvironmentVariableName = "NuGetTestModeEnabled";
        public const string NuGetTestClientName = "NuGet Test Client";

        internal static IEnvironmentVariableReader EnvironmentVariableReader { get; set; } = EnvironmentVariableWrapper.Instance;
        static NuGetTestMode()
        {
            // cached for the life-time of the app domain
            Enabled = FromEnvironmentVariable();
        }

        public static bool Enabled { get; private set; }

        private static bool FromEnvironmentVariable()
        {
            var testMode = EnvironmentVariableReader.GetEnvironmentVariable(_testModeEnvironmentVariableName);
            if (string.IsNullOrEmpty(testMode))
            {
                return false;
            }

            bool isEnabled;
            return bool.TryParse(testMode, out isEnabled) && isEnabled;
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
