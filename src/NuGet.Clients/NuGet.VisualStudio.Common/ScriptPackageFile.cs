using System;
using System.Runtime.Versioning;
using NuGet.Frameworks;
using Utility = System.IO;

namespace NuGet.PackageManagement.VisualStudio
{
    public class ScriptPackageFile : IScriptPackageFile
    {
        public ScriptPackageFile(string path, NuGetFramework targetFramework)
        {
            if (path == null)
            {
                throw new ArgumentNullException("ScriptPackageFilePath");
            }

            if (targetFramework == null)
            {
                throw new ArgumentNullException("ScriptPackageFileTargetFramework");
            }

            Path = path.Replace(Utility.Path.AltDirectorySeparatorChar, Utility.Path.DirectorySeparatorChar);
            TargetFramework = new FrameworkName(targetFramework.DotNetFrameworkName); ;
        }

        // Path is a public API used by init.ps1/install.ps users.
        public string Path
        {
            get;
            set;
        }

        public FrameworkName TargetFramework
        {
            get;
        }
    }
}
