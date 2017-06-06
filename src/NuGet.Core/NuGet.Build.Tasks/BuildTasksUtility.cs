// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using NuGet.ProjectModel;

namespace NuGet.Build.Tasks
{
    public static class BuildTasksUtility
    {
        public static void LogInputParam(Common.ILogger log, string name, params string[] values)
        {
            LogTaskParam(log, "in", name, values);
        }

        public static void LogOutputParam(Common.ILogger log, string name, params string[] values)
        {
            LogTaskParam(log, "out", name, values);
        }

        private static void LogTaskParam(Common.ILogger log, string direction, string name, params string[] values)
        {
            var stringValues = values?.Select(s => s) ?? Enumerable.Empty<string>();

            log.Log(Common.LogLevel.Debug, $"({direction}) {name} '{string.Join(";", stringValues)}'");
        }

        /// <summary>
        /// Add all restorable projects to the restore list.
        /// This is the behavior for --recursive
        /// </summary>
        public static void AddAllProjectsForRestore(DependencyGraphSpec spec)
        {
            // Add everything from projects except for packages.config and unknown project types
            foreach (var project in spec.Projects
                .Where(project => RestorableTypes.Contains(project.RestoreMetadata.ProjectStyle)))
            {
                spec.AddRestore(project.RestoreMetadata.ProjectUniqueName);
            }
        }

        public static void CopyPropertyIfExists(ITaskItem item, IDictionary<string, string> properties, string key)
        {
            CopyPropertyIfExists(item, properties, key, key);
        }

        public static void CopyPropertyIfExists(ITaskItem item, IDictionary<string, string> properties, string key, string toKey)
        {
            var wrapper = new MSBuildTaskItem(item);

            var propertyValue = wrapper.GetProperty(key);

            if (!string.IsNullOrEmpty(propertyValue)
                && !properties.ContainsKey(key))
            {
                properties.Add(toKey, propertyValue);
            }
        }

        public static string GetPropertyIfExists(ITaskItem item, string key)
        {
            var wrapper = new MSBuildTaskItem(item);

            var propertyValue = wrapper.GetProperty(key);

            if (!string.IsNullOrEmpty(propertyValue))
            {
                return propertyValue;
            }

            return null;
        }

        public static void AddPropertyIfExists(IDictionary<string, string> properties, string key, string value)
        {
            if (!string.IsNullOrEmpty(value)
                && !properties.ContainsKey(key))
            {
                properties.Add(key, value);
            }
        }

        public static void AddPropertyIfExists(IDictionary<string, string> properties, string key, string[] value)
        {
            if (value!=null && !properties.ContainsKey(key))
            {
                properties.Add(key, string.Concat(value.Select(e => e + ";")));
            }
        }

        private static HashSet<ProjectStyle> RestorableTypes = new HashSet<ProjectStyle>()
        {
            ProjectStyle.DotnetCliTool,
            ProjectStyle.PackageReference,
            ProjectStyle.Standalone,
            ProjectStyle.ProjectJson
        };
    }
}