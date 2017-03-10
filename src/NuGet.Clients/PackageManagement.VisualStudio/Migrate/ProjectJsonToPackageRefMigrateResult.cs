using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
