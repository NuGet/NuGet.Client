// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace NuGet.ProjectManagement
{
    public interface IPackageFileTransformer
    {
        /// <summary>
        /// Transforms the file
        /// </summary>
        void TransformFile(Func<Stream> fileStreamFactory, string targetPath, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem);

        /// <summary>
        /// Reverses the transform
        /// </summary>
        void RevertFile(Func<Stream> fileStreamFactory, string targetPath, IEnumerable<InternalZipFileInfo> matchingFiles, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem);
    }
}
