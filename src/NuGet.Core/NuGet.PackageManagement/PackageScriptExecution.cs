// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement
{
    internal static class PackageScriptExecution
    {
        // TODO: Change the default to Fail when the remaining callers implement script controls.
        // https://github.com/NuGet/Home/blob/dev/accepted/2026/control-powershell-script-execution.md
        internal const PackageScriptBehavior DefaultBehavior = PackageScriptBehavior.RunWithInit;

        internal static PackageScriptBehavior GetBehavior(INuGetProjectContext context)
        {
            var behavior = NuGet.Shared.NuGetFeatureFlags.IsControlPowerShellScriptExecutionEnabled()
                ? context.ExecutionContext?.PackageScripts ?? DefaultBehavior
                : PackageScriptBehavior.Run;
            if (behavior != PackageScriptBehavior.Fail && behavior != PackageScriptBehavior.Run
                && behavior != PackageScriptBehavior.RunWithInit && behavior != PackageScriptBehavior.Skip)
            {
                throw new ArgumentOutOfRangeException(nameof(context), behavior, null);
            }
            return behavior;
        }

        internal static string? GetScript(
            IEnumerable<FrameworkSpecificGroup> toolGroups,
            NuGetFramework framework,
            string scriptName)
        {
            var group = MSBuildNuGetProjectSystemUtility.Normalize(
                MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(framework, toolGroups));
            return MSBuildNuGetProjectSystemUtility.IsValid(group)
                ? group.Items.FirstOrDefault(path =>
                    path.EndsWith(Path.DirectorySeparatorChar + scriptName, StringComparison.OrdinalIgnoreCase))
                : null;
        }

        internal static void ThrowIfUninstallFilesMissing(
            INuGetProjectContext context,
            PackageIdentity identity,
            string? filePath)
        {
            if (NuGet.Shared.NuGetFeatureFlags.IsControlPowerShellScriptExecutionEnabled())
            {
                var behavior = GetBehavior(context);
                if ((behavior == PackageScriptBehavior.Run || behavior == PackageScriptBehavior.RunWithInit)
                    && !File.Exists(filePath))
                {
                    throw new InvalidOperationException(string.Format(
                        CultureInfo.CurrentCulture, Strings.PackageFilesMissingForScriptExecution, identity));
                }
            }
        }

        internal static void ThrowIfBlocked(INuGetProjectContext context, IEnumerable<string> scripts)
        {
            if (GetBehavior(context) == PackageScriptBehavior.Fail)
            {
                var paths = scripts.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
                if (paths.Count > 0)
                {
                    throw new InvalidOperationException(string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.PackageScriptsBlocked,
                        string.Join(Environment.NewLine, paths)));
                }
            }
        }
    }
}
