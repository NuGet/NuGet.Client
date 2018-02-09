// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NuGet.Common;

namespace NuGet.Packaging.Rules
{
    internal class InitScriptNotUnderToolsRule : IPackageRule
    {
        public IEnumerable<PackLogMessage> Validate(PackageBuilder builder)
        {
            foreach (var file in builder.Files)
            {
                string name = Path.GetFileName(file.Path);
                if (file.TargetFramework != null && name.Equals("init.ps1", StringComparison.OrdinalIgnoreCase))
                {
                    yield return CreatePackageIssue(file);
                }
            }
        }

        private static PackLogMessage CreatePackageIssue(IPackageFile file)
        {
            return PackLogMessage.CreateWarning(
                String.Format(CultureInfo.CurrentCulture, AnalysisResources.MisplacedInitScriptWarning, file.Path),
                NuGetLogCode.NU5107);
        }
    }
}