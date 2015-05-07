// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO.Compression;

namespace NuGet.ProjectManagement
{
    public interface IPackageFileTransformer
    {
        /// <summary>
        /// Transforms the file
        /// </summary>
        void TransformFile(ZipArchiveEntry packageFile, string targetPath, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem);

        /// <summary>
        /// Reverses the transform
        /// </summary>
        void RevertFile(ZipArchiveEntry packageFile, string targetPath, IEnumerable<InternalZipFileInfo> matchingFiles, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem);
    }
}
