using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.PackageManagement.VisualStudio;

namespace NuGet.VisualStudio
{
    class VsProjectJsonMigrateResult : IVsProjectJsonMigrateResult
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
            BackupProjectFile = string.Empty;
            BackupProjectJsonFile = string.Empty;
        }
    }
}
