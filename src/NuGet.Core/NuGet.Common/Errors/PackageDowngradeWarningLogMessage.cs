// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Common
{
    public class PackageDowngradeWarningLogMessage : RestoreLogMessage
    {
        public string DowngradeToDirectPackageRef { get; }

        public string DowngradeFromDirectPackageRef { get; }

        public string MessageWithSolution { get; }

        public PackageDowngradeWarningLogMessage(
            string libraryId,
            string downgradeFrom,
            string downgradeTo,
            string message,
            string messageWithSolution,
            IReadOnlyList<string> targetGraphs) :
            base(LogLevel.Warning, NuGetLogCode.NU1605, message)
        {
            LibraryId = libraryId;
            TargetGraphs = targetGraphs;
            DowngradeToDirectPackageRef = downgradeTo;
            DowngradeFromDirectPackageRef = downgradeFrom;
            MessageWithSolution = messageWithSolution;
        }
    }
}
