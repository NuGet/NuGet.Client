﻿using System;
using System.IO;

namespace NuGet
{
    public static class PackageHelper
    {
        public static bool IsManifest(string path)
        {
            return Path.GetExtension(path).Equals(Constants.ManifestExtension, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPackageFile(string path)
        {
            return Path.GetExtension(path).Equals(Constants.PackageExtension, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsAssembly(string path)
        {
            return path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".winmd", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        }
    }
}
