// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using NuGet.Common;

namespace NuGet.Packaging.Rules
{
    internal class WinRTNameIsObsoleteRule : IPackageRule
    {
        private static string[] Prefixes = new string[]
            { "content\\winrt45\\", "lib\\winrt45\\", "tools\\winrt45\\", "content\\winrt\\", "lib\\winrt\\", "tools\\winrt\\" };

        public IEnumerable<PackageIssueLogMessage> Validate(PackageBuilder builder)
        {
            foreach (var file in builder.Files)
            {
                foreach (string prefix in Prefixes)
                {
                    if (file.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        yield return CreateIssue(file);
                    }
                }
            }
        }

        private static PackageIssueLogMessage CreateIssue(IPackageFile file)
        {
            return new PackageIssueLogMessage(
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.WinRTObsoleteWarning, file.Path),
                NuGetLogCode.NU5106,
                WarningLevel.Default,
                LogLevel.Warning);
        }
    }
}