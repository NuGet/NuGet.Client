// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using Microsoft.Build.Utilities;
using NuGet.Common;

namespace NuGet.Build.Tasks.Pack
{
    public class DefaultSymbolsPackageFormatChangingWarningTask : Task
    {
        public ILogger Logger => new MSBuildLogger(Log);

        public override bool Execute()
        {
            Logger.Log(LogMessage.CreateWarning(NuGetLogCode.NU5132,
                string.Format(CultureInfo.CurrentCulture, Strings.DefaultSymbolsPackageFormatChanging)));
            return true;
        }
    }
}
