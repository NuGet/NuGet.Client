// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace NuGet.ProjectModel
{
    public static class JsonPackageSpecReader
    {
        public static readonly string RestoreOptions = "restore";
        public static readonly string RestoreSettings = "restoreSettings";
        public static readonly string HideWarningsAndErrors = "hideWarningsAndErrors";
        public static readonly string PackOptions = "packOptions";
        public static readonly string PackageType = "packageType";
        public static readonly string Files = "files";

        /// <summary>
        /// Load and parse a project.json file
        /// </summary>
        /// <param name="name">project name</param>
        /// <param name="packageSpecPath">file path</param>
        public static PackageSpec GetPackageSpec(string name, string packageSpecPath)
        {
            return FileUtility.SafeRead(filePath: packageSpecPath, read: (stream, filePath) => GetPackageSpec(stream, name, filePath, null));
        }

        public static PackageSpec GetPackageSpec(string json, string name, string packageSpecPath)
        {
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return GetPackageSpec(ms, name, packageSpecPath, null);
            }
        }

        public static PackageSpec GetPackageSpec(Stream stream, string name, string packageSpecPath, string snapshotValue)
        {
            return GetPackageSpec(stream, name, packageSpecPath, snapshotValue, EnvironmentVariableWrapper.Instance);
        }

        [Obsolete("This method is obsolete and will be removed in a future release.")]
        public static PackageSpec GetPackageSpec(JObject json)
        {
            return GetPackageSpec(json, name: null, packageSpecPath: null, snapshotValue: null);
        }

        [Obsolete("This method is obsolete and will be removed in a future release.")]
        public static PackageSpec GetPackageSpec(JObject rawPackageSpec, string name, string packageSpecPath, string snapshotValue)
        {
            using (var stringReader = new StringReader(rawPackageSpec.ToString()))
            using (var jsonReader = new JsonTextReader(stringReader))
            {
                return NjPackageSpecReader.GetPackageSpec(jsonReader, name, packageSpecPath, snapshotValue);
            }
        }

        [Obsolete]
        internal static PackageSpec GetPackageSpec(JsonTextReader jsonReader, string packageSpecPath)
        {
            return NjPackageSpecReader.GetPackageSpec(jsonReader, packageSpecPath);
        }

        internal static PackageSpec GetPackageSpec(Stream stream, string name, string packageSpecPath, string snapshotValue, IEnvironmentVariableReader environmentVariableReader)
        {
            var useNj = environmentVariableReader.GetEnvironmentVariable("NUGET_EXPERIMENTAL_USE_NJ_FOR_FILE_PARSING");
            if (string.IsNullOrEmpty(useNj) || useNj.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return Utf8JsonStreamPackageSpecReader.GetPackageSpec(stream, name, packageSpecPath, snapshotValue);
            }
            else
            {
                using (var textReader = new StreamReader(stream))
                using (var jsonReader = new JsonTextReader(textReader))
                {
#pragma warning disable CS0612 // Type or member is obsolete
                    return NjPackageSpecReader.GetPackageSpec(jsonReader, name, packageSpecPath, snapshotValue);
#pragma warning restore CS0612 // Type or member is obsolete
                }
            }
        }
    }
}
