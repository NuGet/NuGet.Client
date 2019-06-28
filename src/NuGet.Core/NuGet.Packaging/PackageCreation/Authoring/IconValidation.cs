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
        private const int MaxIconFilzeSize = 1024 * 1024;

        /// <summary>
        /// Validate the icon filesize
        ///
        /// Launches a PackagingException in case of an error
        /// </summary>
        /// <remarks>This consumes the stream</remarks>
        /// <param name="str">Stream that points to the icon file</param>
        public static void ValidateIconFileSize(Stream str)
        {
            long fileSize = EstimateFileSize(str);

            if (fileSize > MaxIconFilzeSize)
            {
                throw new PackagingException(Common.NuGetLogCode.NU5037, NuGetResources.IconMaxFilseSizeExceeded);
            }

            if (fileSize < 0)
            {
                throw new PackagingException(Common.NuGetLogCode.NU5040, NuGetResources.IconErrorReading);
            }

            if (fileSize == 0)
            {
                throw new PackagingException(Common.NuGetLogCode.NU5041, NuGetResources.IconErrorEmpty);
            }
        }

        /// <summary>
        /// Reads up to MaxIconFilzeSize of the file
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        private static long EstimateFileSize(Stream stream)
        {
            long fileSize = -1;
            try
            {
                fileSize = stream.Length;
            }
            catch (NotSupportedException)
            {
                int buffSizeee = MaxIconFilzeSize + 1;
                byte[] byteBuffer = new byte[buffSizeee];

                if (stream.CanRead)
                {
                    fileSize = stream.Read(byteBuffer, 0, buffSizeee);
                }
            }

            return fileSize;
        }
    }
}
