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
        internal static PackageScriptBehavior GetBehavior(INuGetProjectContext context)
        {
            var behavior = NuGet.Shared.PowerShellScriptExecutionFeature.IsEnabled
                ? context.ExecutionContext?.PackageScripts ?? PackageScriptBehavior.Fail
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
            if (NuGet.Shared.PowerShellScriptExecutionFeature.IsEnabled)
            {
                var behavior = GetBehavior(context);
                if ((behavior == PackageScriptBehavior.Run || behavior == PackageScriptBehavior.RunWithInit)
                    && !File.Exists(filePath))
                {
                    throw new InvalidOperationException(string.Format(
                        CultureInfo.CurrentCulture, Strings.PackageFilesMissingForUninstall, identity));
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
