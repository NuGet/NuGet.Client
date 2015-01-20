using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.VisualStudio
{
    public class PackageOperationEventArgs : CancelEventArgs
    {
        public PackageOperationEventArgs(string installPath, PackageIdentity package, string fileSystemRoot)
        {
            Package = package;
            InstallPath = installPath;
            //FileSystem = fileSystem;
            FileSystemRoot = fileSystemRoot;
        }

        public string InstallPath { get; private set; }
        public PackageIdentity Package { get; private set; }
        //public IPackage Package { get; private set; }
        //public IFileSystem FileSystem { get; private set; }
        public string FileSystemRoot { get; private set; }
    }
}
