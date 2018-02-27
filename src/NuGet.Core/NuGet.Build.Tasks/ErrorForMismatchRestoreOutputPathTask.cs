using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Commands;

namespace NuGet.Build.Tasks
{
    public class ErrorForMismatchRestoreOutputPathTask : Task
    {
        [Required]
        public string RestoreOutputAbsolutePath { get; set; }

        [Required]
        public string MSBuildProjectExtensionsPath { get; set; }

        public override bool Execute()
        {
            if (RestoreOutputAbsolutePath != MSBuildProjectExtensionsPath)
            {
                //  The RestoreOutputPath, which resolved to '{0}', did not match the MSBuildProjectExtensionsPath, which was '{1}'.  These properties must match in order for assets from NuGet restore to be applied correctly when building.
                var log = new MSBuildLogger(Log);
                var message = MSBuildRestoreUtility.GetErrorForRestoreOutputPathMismatch(RestoreOutputAbsolutePath, MSBuildProjectExtensionsPath);
                log.Log(message);
            }
            
            return true;
        }
    }
}
