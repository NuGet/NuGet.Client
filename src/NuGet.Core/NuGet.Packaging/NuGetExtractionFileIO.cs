// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet.Packaging
{
    internal static class NuGetExtractionFileIO
    {
        internal static FileStream CreateFile(string path)
        {
            // Entry permissions are not restored to maintain backwards compatibility with .NET Core 1.x.
            // (https://github.com/NuGet/Home/issues/4424)
            // On .NET Core 1.x, all extracted files had default permissions of 766.
            // The default on .NET Core 2.x has changed to 666.
            // To avoid breaking executable files in existing packages (which don't have the x-bit set)
            // we force the .NET Core 1.x default permissions.
#if NET
            if (!System.OperatingSystem.IsWindows())
            {
                // Match creat's write-only descriptor without restricting other processes from opening the file.
                return new FileStream(
                    path,
                    new FileStreamOptions
                    {
                        Mode = FileMode.Create,
                        Access = FileAccess.Write,
                        Share = FileShare.ReadWrite | FileShare.Delete,
                        UnixCreateMode =
                            UnixFileMode.UserRead |
                            UnixFileMode.UserWrite |
                            UnixFileMode.UserExecute |
                            UnixFileMode.GroupRead |
                            UnixFileMode.GroupWrite |
                            UnixFileMode.OtherRead |
                            UnixFileMode.OtherWrite
                    });
            }
#endif
            return File.Create(path);
        }
    }
}
