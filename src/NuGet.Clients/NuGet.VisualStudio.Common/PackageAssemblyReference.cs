// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet.PackageManagement.VisualStudio
{
    internal class PackageAssemblyReference : IPackageAssemblyReference
    {
        private string _path;

        public PackageAssemblyReference(string path)
        {
            _path = path;
        }

        public string Name
        {
            get
            {
                return Path.GetFileName(_path);
            }
        }
    }
}
