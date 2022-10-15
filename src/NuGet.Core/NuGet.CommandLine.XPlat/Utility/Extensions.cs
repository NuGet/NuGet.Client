// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.CommandLine.XPlat.Utility
{
    internal static class Extensions
    {
        public static string NormalizeFilePath(this string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

#if NETCOREAPP
            return path.Replace("\\", "/", StringComparison.Ordinal);
#else
            return path.Replace("\\", "/");
#endif
        }
    }
}
