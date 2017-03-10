// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.PackageManagement.VisualStudio
{
    public class ProjectJsonToPackageRefMigrateResult
    {
        public bool IsSuccess { get; }

        public string BackupProjectFile { get; }

        public string BackupProjectJsonFile { get; }

        public ProjectJsonToPackageRefMigrateResult(bool isSuccess, string backupProjectFile, string backupProjectJsonFile)
        {
            IsSuccess = isSuccess;
            BackupProjectFile = backupProjectFile;
            BackupProjectJsonFile = backupProjectJsonFile;
        }
    }
}
