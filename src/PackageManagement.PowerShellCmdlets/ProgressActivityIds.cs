using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    internal static class ProgressActivityIds
    {

        // represents the activity Id for the Get-Package command to report its progress
        public const int GetPackageId = 1;

        // represents the activity Id for download progress operation
        public const int DownloadPackageId = 2;
    }
}
