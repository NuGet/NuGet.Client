// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Shared;

namespace NuGet.Commands
{
    /// <summary>
    /// Class to hold ProjectWide and PackageSpecific WarningProperties.
    /// </summary>
    public class WarningPropertiesCollection
    {
        private readonly ConcurrentDictionary<string, NuGetFramework> _getFrameworkCache = new ConcurrentDictionary<string, NuGetFramework>();

        /// <summary>
        /// Contains the target frameworks for the project.
        /// These are used for no warn filtering in case of a log message without a target graph.
        /// </summary>
        public IReadOnlyList<NuGetFramework> ProjectFrameworks { get; set; } = new List<NuGetFramework>();

        /// <summary>
        /// Contains Project wide properties for Warnings.
        /// </summary>
        public WarningProperties ProjectWideWarningProperties { get; set; }

        /// <summary>
        /// Contains Package specific properties for Warnings.
        /// NuGetLogCode -> LibraryId -> Set of Frameworks.
        /// </summary>
        public PackageSpecificWarningProperties PackageSpecificWarningProperties { get; set; }

        /// <summary>
        /// Attempts to suppress a warning log message or upgrade it to error log message.
        /// The decision is made based on the Package Specific or Project wide warning properties.
        /// </summary>
        /// <param name="message">Message that should be suppressed or upgraded to an error.</param>
        /// <returns>Bool indicating is the warning should be suppressed or not. 
        /// If not then the param message sould have been mutated to an error</returns>
        public bool ApplyWarningProperties(IRestoreLogMessage message)
        {
            if (message.Level != LogLevel.Warning)
            {
                return false;
            }
            else
            {
                // First look at PackageSpecificWarningProperties and then at ProjectWideWarningProperties

                if (!string.IsNullOrEmpty(message.LibraryId) && PackageSpecificWarningProperties != null)
                {
                    var messageTargetFrameworks = message.TargetGraphs.Select(GetNuGetFramework).ToList();

                    // The message does not contain a LibraryId then assign all the project frameworks to it.
                    if (messageTargetFrameworks.Count == 0)
                    {
                        return ProjectFrameworks
                            .All(e => PackageSpecificWarningProperties.Contains(message.Code, message.LibraryId, e));
                    }
                    else
                    {
                        message.TargetGraphs = message.TargetGraphs
                            .Where(e => !PackageSpecificWarningProperties.Contains(message.Code, message.LibraryId, GetNuGetFramework(e))).ToList();

                        if (message.TargetGraphs.Count == 0)
                        {
                            return true;
                        }
                    }

                }

                // The message does not contain a LibraryId or it is not suppressed in package specific settings
                // Use ProjectWideWarningProperties
                return ProjectWideWarningProperties != null && ApplyProjectWideWarningProperties(message);
            }
        }

        /// <summary>
        /// Method is used to check is a warning should be suppressed and if not then if it should be treated as an error.
        /// </summary>
        /// <param name="logMessage">Message which should be mutated if needed.</param>
        /// <returns>bool indicating if the ILogMessage should be suppressed or not.</returns>
        private bool ApplyProjectWideWarningProperties(ILogMessage logMessage)
        {
            if (logMessage.Level == LogLevel.Warning)
            {
                if (ProjectWideWarningProperties.NoWarn.Contains(logMessage.Code))
                {
                    return true;
                }
                else if (ProjectWideWarningProperties.AllWarningsAsErrors && logMessage.Code > NuGetLogCode.Undefined || 
                    ProjectWideWarningProperties.WarningsAsErrors.Contains(logMessage.Code))
                {
                    logMessage.Level = LogLevel.Error;
                    return false;
                }
            }
            return false;
        }

        private NuGetFramework GetNuGetFramework(string targetGraph)
        {
            return _getFrameworkCache.GetOrAdd(targetGraph, (s) => GetNuGetFrameworkFromTargetGraph(s));
        }

        private static NuGetFramework GetNuGetFrameworkFromTargetGraph(string targetGraph)
        {
            var parts = targetGraph.Split('/');

            return NuGetFramework.Parse(parts[0]);
        }
    }
}
