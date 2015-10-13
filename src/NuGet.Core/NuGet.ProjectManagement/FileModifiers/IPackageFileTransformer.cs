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
        void TransformFile(Func<Stream> fileStreamFactory, string targetPath, IMSBuildNuGetProjectSystem projectSystem);

        /// <summary>
        /// Reverses the transform on the targetPath, using all the potential source of change
        /// </summary>
        /// <param name="fileStreamFactory">A factory for accessing the file to be reverted from the nupkg being uninstalled.</param>
        /// <param name="targetPath">A path to the file to be reverted.</param>
        /// <param name="matchingFiles">Other files in other packages that may have changed the <paramref name="targetPath"/>.</param>
        /// <param name="projectSystem">The project where this change is taking place.</param>
        void RevertFile(Func<Stream> fileStreamFactory,
            string targetPath,
            IEnumerable<InternalZipFileInfo> matchingFiles,
            IMSBuildNuGetProjectSystem projectSystem);
    }
}
