// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Common
{
    /// <summary>
    /// Class to hold warning properties given by project system.
    /// </summary>
    public class WarningProperties
    {
        /// <summary>
        /// List of Warning Codes that should be treated as Errors.
        /// </summary>
        public ISet<NuGetLogCode> WarningsAsErrorsSet { get; } = new HashSet<NuGetLogCode>();

        /// <summary>
        /// List of Warning Codes that should be ignored.
        /// </summary>
        public ISet<NuGetLogCode> NoWarnSet { get; } = new HashSet<NuGetLogCode>();

        /// <summary>
        /// Indicates if all warnings should be ignored.
        /// </summary>
        public bool AllWarningsAsErrors { get; } = false;

        public WarningProperties(ISet<NuGetLogCode> warningsAsErrorsSet, ISet<NuGetLogCode> noWarnSet, bool allWarningsAsErrors)
        {
            WarningsAsErrorsSet = warningsAsErrorsSet;
            NoWarnSet = noWarnSet;
            AllWarningsAsErrors = allWarningsAsErrors;
        }

        /// <summary>
        /// Method is used to check is a warning should be suppressed and if not then if it should be treated as an error.
        /// </summary>
        /// <param name="logMessage">Message which should be mutated if needed.</param>
        /// <returns>bool indicating if the ILogMessage should be suppressed or not.</returns>
        public bool TrySuppressWarning(ILogMessage logMessage)
        {
            if (logMessage.Level == LogLevel.Warning)
            {
                if (NoWarnSet.Contains(logMessage.Code))
                {
                    return true;
                }
                else if (AllWarningsAsErrors || WarningsAsErrorsSet.Contains(logMessage.Code))
                {
                    logMessage.Level = LogLevel.Error;
                    return false;
                }
            }
            return false;
        }
    }
}
