// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    public class DotnetDeprecatedPrompt
    {
        private const string DotnetDeprecationUrl = "https://aka.ms/rugr4c";

        /// <summary>
        /// This is the registry key which tracks whether to show the deprecated framework window.
        /// </summary>
        private const string DoNotShowDeprecatedFrameworkWindowRegistryName = "DoNotShowDeprecatedFrameworkWindow";

        public static DeprecatedFrameworkModel GetDeprecatedFrameworkModel(IEnumerable<NuGetProject> affectedProjects)
        {
            return new DeprecatedFrameworkModel(
                FrameworkConstants.CommonFrameworks.DotNet,
                DotnetDeprecationUrl,
                affectedProjects);
        }

        public static IEnumerable<NuGetProject> GetAffectedProjects(IEnumerable<ResolvedAction> actions)
        {
            var projects = new HashSet<NuGetProject>();

            foreach (var action in actions)
            {
                var buildIntegrationAction = action.Action as BuildIntegratedProjectAction;

                if (buildIntegrationAction == null || buildIntegrationAction.RestoreResult.Success)
                {
                    continue;
                }

                // Get all failed compatibility check results.
                var incompatible = buildIntegrationAction
                    .RestoreResult
                    .CompatibilityCheckResults
                    .Where(result => !result.Success && result.Issues.Any());

                // Only focus on compatibility check results when restoring for "dotnet".
                var anyIncompatibleDotnet = incompatible.Any(result => string.Equals(
                    result.Graph.Framework.Framework,
                    FrameworkConstants.FrameworkIdentifiers.NetPlatform,
                    StringComparison.OrdinalIgnoreCase));

                if (anyIncompatibleDotnet)
                {
                    projects.Add(action.Project);
                }
            }

            return projects;
        }

        public static void SaveDoNotShowPromptState(bool doNotshow)
        {
            RegistrySettingUtility.SetBooleanSetting(
                DoNotShowDeprecatedFrameworkWindowRegistryName,
                doNotshow);
        }

        public static bool GetDoNotShowPromptState()
        {
            return RegistrySettingUtility.GetBooleanSetting(
                DoNotShowDeprecatedFrameworkWindowRegistryName);
        }
    }
}
