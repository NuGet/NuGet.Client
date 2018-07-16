// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.ProjectModel
{
    public class PackageDowngradeAssetsLogMessage : AssetsLogMessage
    {
        public string DowngradeToDirectPackageRef { get; }

        public string DowngradeFromDirectPackageRef { get; }

        public string MessageWithSolution { get; }

        public string LibaryIdHigherVersion { get; }

        public PackageDowngradeAssetsLogMessage(PackageDowngradeWarningLogMessage logMessage)
            : base(logMessage.Level, logMessage.Code, logMessage.Message)
        {
            ProjectPath = logMessage.ProjectPath;
            WarningLevel = logMessage.WarningLevel;
            FilePath = logMessage.FilePath;
            LibraryId = logMessage.LibraryId;
            TargetGraphs = logMessage.TargetGraphs;
            StartLineNumber = logMessage.StartLineNumber;
            StartColumnNumber = logMessage.StartColumnNumber;
            EndLineNumber = logMessage.EndLineNumber;
            EndColumnNumber = logMessage.EndColumnNumber;
            DowngradeFromDirectPackageRef = logMessage.DowngradeFromDirectPackageRef;
            DowngradeToDirectPackageRef = logMessage.DowngradeToDirectPackageRef;
            MessageWithSolution = logMessage.MessageWithSolution;
            LibaryIdHigherVersion = logMessage.LibaryIdHigherVersion;
        }

        public override RestoreLogMessage AsRestoreLogMessage()
        {
            return new PackageDowngradeWarningLogMessage(LibraryId, LibaryIdHigherVersion, DowngradeFromDirectPackageRef, DowngradeToDirectPackageRef, Message, MessageWithSolution, TargetGraphs)
            {
                ProjectPath = ProjectPath,
                WarningLevel = WarningLevel,
                FilePath = FilePath,
                StartLineNumber = StartLineNumber,
                StartColumnNumber = StartColumnNumber,
                EndLineNumber = EndLineNumber,
                EndColumnNumber = EndColumnNumber
            };
        }
    }
}
