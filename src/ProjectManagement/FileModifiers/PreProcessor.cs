// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Simple token replacement system for content files.
    /// </summary>
    public class Preprocessor : IPackageFileTransformer
    {
        public void TransformFile(Func<Stream> fileStreamFactory, string targetPath, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            MSBuildNuGetProjectSystemUtility.TryAddFile(msBuildNuGetProjectSystem, targetPath,
                () => StreamUtility.StreamFromString(Process(fileStreamFactory, msBuildNuGetProjectSystem)));
        }

        public void RevertFile(Func<Stream> fileStreamFactory, string targetPath, IEnumerable<InternalZipFileInfo> matchingFiles, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            MSBuildNuGetProjectSystemUtility.DeleteFileSafe(targetPath,
                () => StreamUtility.StreamFromString(Process(fileStreamFactory, msBuildNuGetProjectSystem)),
                msBuildNuGetProjectSystem);
        }

        internal static string Process(Func<Stream> fileStreamFactory, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            using (var stream = fileStreamFactory())
            {
                return Process(stream, msBuildNuGetProjectSystem, throwIfNotFound: false);
            }
        }

        public static string Process(Stream stream, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem, bool throwIfNotFound = true)
        {
            string text;
            using (var streamReader = new StreamReader(stream))
            {
                text = streamReader.ReadToEnd();
            }
            var tokenizer = new Tokenizer(text);
            var result = new StringBuilder();
            for (;;)
            {
                var token = tokenizer.Read();
                if (token == null)
                {
                    break;
                }

                if (token.Category == TokenCategory.Variable)
                {
                    var replaced = ReplaceToken(token.Value, msBuildNuGetProjectSystem, throwIfNotFound);
                    result.Append(replaced);
                }
                else
                {
                    result.Append(token.Value);
                }
            }

            return result.ToString();
        }

        private static string ReplaceToken(string propertyName, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem, bool throwIfNotFound)
        {
            var value = msBuildNuGetProjectSystem.GetPropertyValue(propertyName);
            if (value == null && throwIfNotFound)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.TokenHasNoValue, propertyName));
            }
            return value;
        }
    }
}
