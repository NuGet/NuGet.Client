// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Packaging.Core;
using NuGet.Packaging.PackageCreation.Resources;

namespace NuGet.Packaging
{
    /// <summary>
    /// Utility methods for package icon validation
    /// </summary>
    public class IconValidation
    {
        /// <summary>
        /// Maximun Icon file size: 1 megabyte
        /// </summary>
        public const int MaxIconFileSize = 1024 * 1024;

        /// <summary>
        /// Validate the icon filesize
        ///
        /// Launches a PackagingException in case of an error
        ///
        /// This validation is intended to run at pre-packing time
        /// </summary>
        /// <remarks>This consumes the stream</remarks>
        /// <param name="stream">Stream that points to the icon file</param>
        /// <exception cref="PackagingException">When the icon file size is:
        /// 1) Greater than the maximum icon file size <see cref="MaxIconFileSize"/>
        /// 2) Less than zero, indicating an error reading the icon file
        /// 3) Zero, indicating an empty file
        /// </exception>
        public static void ValidateIconFileSize(Stream stream)
        {
            long fileSize = stream.Length;

            if (fileSize > MaxIconFileSize)
            {
                throw new PackagingException(Common.NuGetLogCode.NU5037, NuGetResources.IconMaxFilseSizeExceeded);
            }

            if (fileSize == 0)
            {
                throw new PackagingException(Common.NuGetLogCode.NU5041, NuGetResources.IconErrorEmpty);
            }
        }
    }
}
