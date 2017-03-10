// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.PackageManagement.VisualStudio;

namespace NuGet.VisualStudio
{
    internal class VsProjectJsonMigrateResult : IVsProjectJsonMigrateResult
    {
        public bool IsSuccess { get; }

        public string BackupProjectFile { get; }

        public string BackupProjectJsonFile { get; }

        public string ErrorMessage { get; }

        public VsProjectJsonMigrateResult(ProjectJsonToPackageRefMigrateResult result)
        {
            IsSuccess = result.IsSuccess;
            BackupProjectFile = result.BackupProjectFile;
            BackupProjectJsonFile = result.BackupProjectJsonFile;
        }

        public VsProjectJsonMigrateResult(string errorMessage)
        {
            IsSuccess = false;
            ErrorMessage = errorMessage;
        }
    }
}
