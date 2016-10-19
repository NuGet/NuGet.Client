// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet.ProjectModel
{
    public static class DotnetCliToolPathResolver
    {
        /// <summary>
        /// Tool file extension
        /// </summary>
        public static readonly string Extension = ".dotnetclitool.json";

        /// <summary>
        /// Gives the full path to the tool file.
        /// </summary>
        public static string GetFilePath(string projectOutputDirectory, string packageId)
        {
            return Path.GetFullPath(Path.Combine(projectOutputDirectory, GetFileName(packageId)));
        }

        /// <summary>
        /// Gives the tool file name. Ex: toola.dotnetclitool.json
        /// </summary>
        public static string GetFileName(string packageId)
        {
            return $"{packageId.ToLowerInvariant()}{Extension}";
        }
    }
}