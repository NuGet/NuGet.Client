// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.Commands.PackCommand
{
    /// <summary>
    /// Class to hold PackageSpecific WarningProperties.
    /// </summary>
    public class WarningPropertiesCollection
    {
        /// <summary>
        /// Contains Package specific properties for Warnings.
        /// NuGetLogCode -> LibraryId -> Set of Frameworks.
        /// </summary>
        internal PackageSpecificWarningProperties PackageSpecificWarningProperties { get; }

        public WarningPropertiesCollection(PackageSpecificWarningProperties packageSpecificWarningProperties)
        {
            PackageSpecificWarningProperties = packageSpecificWarningProperties;
        }

        /// <summary>
        /// Attempts to suppress a warning log message.
        /// The decision is made based on the Package Specific no warn properties.
        /// </summary>
        /// <param name="message">Message that should be suppressed.</param>
        /// <returns>Bool indicating is the warning should be suppressed or not.</returns>
        internal bool ApplyNoWarnProperties(IPackLogMessage message)
        {
            return ApplyPackageSpecificNoWarnProperties(message);
        }

        /// <summary>
        /// Method is used to check is a warning should be suppressed due to package specific no warn properties.
        /// </summary>
        /// <param name="message">Message to be checked for no warn.</param>
        /// <returns>bool indicating if the IRestoreLogMessage should be suppressed or not.</returns>
        private bool ApplyPackageSpecificNoWarnProperties(IPackLogMessage message)
        {
            if (message.Level == LogLevel.Warning &&
                PackageSpecificWarningProperties != null &&
                !string.IsNullOrEmpty(message.LibraryId) &&
                message.Framework != null)
            {
                // Suppress the warning if the code + libraryId combination is suppressed for given framework.
                if (PackageSpecificWarningProperties.Contains(message.Code, message.LibraryId, message.Framework))
                {
                    return true;
                }
            }

            // The message is not a warning or it does not contain a LibraryId or it is not suppressed in package specific settings.
            return false;
        }
    }
}
