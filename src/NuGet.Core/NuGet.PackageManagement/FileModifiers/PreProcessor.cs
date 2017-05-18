// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Configuration;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Simple token replacement system for content files.
    /// </summary>
    public class Preprocessor : IPackageFileTransformer
    {
        public void TransformFile(Func<Stream> fileStreamFactory, string targetPath, IMSBuildProjectSystem msBuildNuGetProjectSystem)
        {
            MSBuildNuGetProjectSystemUtility.TryAddFile(msBuildNuGetProjectSystem, targetPath,
                () => StreamUtility.StreamFromString(Process(fileStreamFactory, msBuildNuGetProjectSystem)));
        }

        public void RevertFile(Func<Stream> fileStreamFactory, string targetPath, IEnumerable<InternalZipFileInfo> matchingFiles, IMSBuildProjectSystem msBuildNuGetProjectSystem)
        {
            MSBuildNuGetProjectSystemUtility.DeleteFileSafe(targetPath,
                () => StreamUtility.StreamFromString(Process(fileStreamFactory, msBuildNuGetProjectSystem)),
                msBuildNuGetProjectSystem);
        }

        internal static string Process(Func<Stream> fileStreamFactory, IMSBuildProjectSystem msBuildNuGetProjectSystem)
        {
            return NuGet.Common.Preprocessor.Process(fileStreamFactory, propName => msBuildNuGetProjectSystem.GetPropertyValue(propName));
        }
    }
}
