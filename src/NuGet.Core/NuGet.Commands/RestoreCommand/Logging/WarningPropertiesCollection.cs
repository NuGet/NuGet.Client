// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public class WarningPropertiesCollection : IEquatable<WarningPropertiesCollection>
    {
        private readonly ConcurrentDictionary<string, NuGetFramework> _getFrameworkCache = new ConcurrentDictionary<string, NuGetFramework>();

        /// <summary>
        /// Contains the target frameworks for the project.
        /// These are used for no warn filtering in case of a log message without a target graph.
        /// </summary>
        public IReadOnlyList<NuGetFramework> ProjectFrameworks { get; }

        /// <summary>
        /// Contains Project wide properties for Warnings.
        /// </summary>
        public WarningProperties ProjectWideWarningProperties { get; }

        /// <summary>
        /// Contains Package specific properties for Warnings.
        /// NuGetLogCode -> LibraryId -> Set of Frameworks.
        /// </summary>
        public PackageSpecificWarningProperties PackageSpecificWarningProperties { get; }

        public WarningPropertiesCollection(WarningProperties projectWideWarningProperties,
            PackageSpecificWarningProperties packageSpecificWarningProperties,
            IReadOnlyList<NuGetFramework> projectFrameworks)
        {
            ProjectWideWarningProperties = projectWideWarningProperties;
            PackageSpecificWarningProperties = packageSpecificWarningProperties;
            ProjectFrameworks = projectFrameworks ?? new ReadOnlyCollection<NuGetFramework>(new List<NuGetFramework>());
        }

        /// <summary>
        /// Attempts to suppress a warning log message or upgrade it to error log message.
        /// The decision is made based on the Package Specific or Project wide warning properties.
        /// </summary>
        /// <param name="message">Message that should be suppressed or upgraded to an error.</param>
        /// <returns>Bool indicating is the warning should be suppressed or not. 
        /// If not then the param message sould have been mutated to an error</returns>
        public bool ApplyWarningProperties(IRestoreLogMessage message)
        {
            if (ApplyProjectWideNoWarnProperties(message) || ApplyPackageSpecificNoWarnProperties(message))
            {
                return true;
            }
            else
            {
                ApplyWarningAsErrorProperties(message);
                return false;
            }
        }

        /// <summary>
        /// Attempts to suppress a warning log message.
        /// The decision is made based on the Package Specific or Project wide no warn properties.
        /// </summary>
        /// <param name="message">Message that should be suppressed.</param>
        /// <returns>Bool indicating is the warning should be suppressed or not.</returns>
        public bool ApplyNoWarnProperties(IRestoreLogMessage message)
        {
            return ApplyProjectWideNoWarnProperties(message) || ApplyPackageSpecificNoWarnProperties(message);
        }

        /// <summary>
        /// Method is used to upgrade a warning to an error if needed.
        /// </summary>
        /// <param name="message">Message which should be upgraded to error if needed.</param>
        public void ApplyWarningAsErrorProperties(IRestoreLogMessage message)
        {
            ApplyProjectWideWarningsAsErrorProperties(message);
        }

        /// <summary>
        /// Method is used to check is a warning should be suppressed due to package specific no warn properties.
        /// </summary>
        /// <param name="message">Message to be checked for no warn.</param>
        /// <returns>bool indicating if the IRestoreLogMessage should be suppressed or not.</returns>
        private bool ApplyPackageSpecificNoWarnProperties(IRestoreLogMessage message)
        {            
            if (message.Level == LogLevel.Warning &&
                PackageSpecificWarningProperties != null &&
                !string.IsNullOrEmpty(message.LibraryId))
            {
                var messageTargetFrameworks = message.TargetGraphs.Select(GetNuGetFramework).ToList();

                // If the message does not contain a target graph, assume that it is applicable for all project frameworks.
                if (messageTargetFrameworks.Count == 0)
                {
                    // Suppress the warning if the code + libraryId combination is suppressed for all project frameworks.
                    if (ProjectFrameworks.Count > 0 &&
                        ProjectFrameworks.All(e => PackageSpecificWarningProperties.Contains(message.Code, message.LibraryId, e)))
                    {
                        return true;
                    }
                }
                else
                {
                    // Get all the target graphs for which code + libraryId combination is not suppressed.
                    message.TargetGraphs = message
                        .TargetGraphs
                        .Where(e => !PackageSpecificWarningProperties.Contains(message.Code, message.LibraryId, GetNuGetFramework(e)))
                        .ToList();

                    // If the message is left with no target graphs then suppress it.
                    if (message.TargetGraphs.Count == 0)
                    {
                        return true;
                    }
                }
            }

            // The message is not a warning or it does not contain a LibraryId or it is not suppressed in package specific settings.
            return false;
        }

        /// <summary>
        /// Method is used to check is a warning should be suppressed due to project wide no warn properties.
        /// </summary>
        /// <param name="message">Message to be checked for no warn.</param>
        /// <returns>bool indicating if the ILogMessage should be suppressed or not.</returns>
        private bool ApplyProjectWideNoWarnProperties(ILogMessage message)
        {
            if (message.Level == LogLevel.Warning && ProjectWideWarningProperties != null)
            {
                if (ProjectWideWarningProperties.NoWarn.Contains(message.Code))
                {
                    // If the project wide NoWarn contains the message code then suppress it.
                    return true;
                }              
            }

            // the project wide NoWarn does contain the message code. do not suppress the warning.
            return false;
        }

        /// <summary>
        /// Method is used to check is a warning should be treated as an error.
        /// </summary>
        /// <param name="message">Message which should be upgraded to error if needed.</param>
        private void ApplyProjectWideWarningsAsErrorProperties(ILogMessage message)
        {
            if (message.Level == LogLevel.Warning && ProjectWideWarningProperties != null)
            {
                if ((ProjectWideWarningProperties.AllWarningsAsErrors && message.Code > NuGetLogCode.Undefined) || 
                    ProjectWideWarningProperties.WarningsAsErrors.Contains(message.Code))
                {
                    // If the project wide AllWarningsAsErrors is true and the message has a valid code or
                    // Project wide WarningsAsErrors contains the message code then upgrade to error.
                    message.Level = LogLevel.Error;
                }
            }
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

        public override int GetHashCode()
        {
            var hashCode = new HashCodeCombiner();

            hashCode.AddObject(ProjectWideWarningProperties);
            hashCode.AddObject(PackageSpecificWarningProperties);
            hashCode.AddSequence(ProjectFrameworks);

            return hashCode.CombinedHash;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WarningPropertiesCollection);
        }

        public bool Equals(WarningPropertiesCollection other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return EqualityUtility.EqualsWithNullCheck(ProjectWideWarningProperties, other.ProjectWideWarningProperties) &&
                EqualityUtility.EqualsWithNullCheck(PackageSpecificWarningProperties, other.PackageSpecificWarningProperties) &&
                EqualityUtility.OrderedEquals(ProjectFrameworks, other.ProjectFrameworks, (fx) => fx.Framework, orderComparer: StringComparer.OrdinalIgnoreCase, sequenceComparer: new NuGetFrameworkFullComparer());
        }
    }
}
