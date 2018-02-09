// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Common;
using NuGet.ProjectModel;

namespace NuGet.Commands
{
    public class PackCollectorLogger : LoggerBase
    {
        public WarningProperties WarningProperties { get; set; }
        private ILogger _innerLogger;
        public PackCollectorLogger(ILogger innerLogger, WarningProperties warningProperties)
        {
            _innerLogger = innerLogger;
            WarningProperties = warningProperties;
        }

        public override void Log(ILogMessage message)
        {
            // check if the message is a warning and it is suppressed 
            if (!IsWarningSuppressed(message))
            {
                // if the message is not suppressed then check if it needs to be upgraded to an error
                UpgradeWarningToErrorIfNeeded(message);

                if (DisplayMessage(message))
                {
                    _innerLogger.Log(message);
                }
            }
        }

        public override Task LogAsync(ILogMessage message)
        {
            // check if the message is a warning and it is suppressed 
            if (!IsWarningSuppressed(message))
            {
                // if the message is not suppressed then check if it needs to be upgraded to an error
                UpgradeWarningToErrorIfNeeded(message);

                if (DisplayMessage(message))
                {
                    return _innerLogger.LogAsync(message);
                }
            }

            return Task.FromResult(0);
        }

        private bool IsWarningSuppressed(ILogMessage message)
        {
            if (message.Level == LogLevel.Warning)
            {
                return WarningPropertiesCollection.ApplyProjectWideNoWarnProperties(message, warningProperties: WarningProperties);
            }

            return false;
        }

        /// <summary>
        /// This method upgrades the warning to an error if the project wide warning properties have set the code in WarningsAsErrors or
        /// set TreatWarningsAsErrors to true
        /// </summary>
        /// <param name="message">ILogMessage to be logged as an error or warning.</param>
        /// <returns>bool indicating if the message should be suppressed.</returns>
        private void UpgradeWarningToErrorIfNeeded(ILogMessage message)
        {
            WarningPropertiesCollection.ApplyProjectWideWarningsAsErrorProperties(message, WarningProperties);
        }

        /// <summary>
        /// Decides if the log should be passed to the inner logger.
        /// </summary>
        /// <param name="message">ILogMessage to be logged.</param>
        /// <returns>bool indicating if this message should be logged.</returns>
        private bool DisplayMessage(ILogMessage message)
        {
            return (message.Level >= VerbosityLevel);
        }
    }
}