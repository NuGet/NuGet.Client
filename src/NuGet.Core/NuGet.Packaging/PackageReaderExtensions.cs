// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Packaging.Core;

namespace NuGet.Packaging
{
    public static class PackageReaderExtensions
    {
        public static IEnumerable<string> GetPackageFiles(this IPackageCoreReader packageReader, PackageSaveMode packageSaveMode)
        {
            return packageReader
                .GetFiles()
                .Where(file => PackageHelper.IsPackageFile(file, packageSaveMode));
        }

        public static IEnumerable<string> GetSatelliteFiles(this IPackageContentReader packageReader, string packageLanguage)
        {
            var satelliteFiles = new List<string>();

            // Existence of the package file is the validation that the package exists
            var libItemGroups = packageReader.GetLibItems();
            foreach (var libItemGroup in libItemGroups)
            {
                var satelliteFilesInGroup = libItemGroup.Items.Where(item => Path.GetDirectoryName(item).Split(Path.DirectorySeparatorChar)
                    .Contains(packageLanguage, StringComparer.OrdinalIgnoreCase));

                satelliteFiles.AddRange(satelliteFilesInGroup);
            }

            return satelliteFiles;
        }
    }
}
