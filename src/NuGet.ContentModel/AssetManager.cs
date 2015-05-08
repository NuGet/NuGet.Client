// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;

namespace NuGet.ContentModel
{
    public static class AssetManager
    {
        internal static IEnumerable<Asset> GetPackageAssets(string packageDirectory)
        {
            packageDirectory = EnsureTrailingSlash(packageDirectory);

            foreach (var path in Directory.EnumerateFiles(packageDirectory, "*.*", SearchOption.AllDirectories))
            {
                var item = new Asset();
                if (Path.GetExtension(path) == ".nuspec"
                    ||
                    Path.GetExtension(path) == ".nupkg"
                    ||
                    Path.GetExtension(path) == ".sha512")
                {
                    continue;
                }

                item.Path = path.Substring(packageDirectory.Length).Replace('\\', '/');
                yield return item;
            }
        }

        private static string EnsureTrailingSlash(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
