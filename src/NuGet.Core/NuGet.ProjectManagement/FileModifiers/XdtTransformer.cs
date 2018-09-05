// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using Microsoft.Web.XmlTransform;

namespace NuGet.ProjectManagement
{
    public class XdtTransformer : IPackageFileTransformer
    {
        public void TransformFile(Func<Stream> fileStreamFactory, string targetPath, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            PerformXdtTransform(fileStreamFactory, targetPath, msBuildNuGetProjectSystem);
        }

        public void RevertFile(Func<Stream> fileStreamFactory, string targetPath, IEnumerable<InternalZipFileInfo> matchingFiles, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            PerformXdtTransform(fileStreamFactory, targetPath, msBuildNuGetProjectSystem);
        }

        private static void PerformXdtTransform(Func<Stream> fileStreamFactory, string targetPath, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            if (FileSystemUtility.FileExists(msBuildNuGetProjectSystem.ProjectFullPath, targetPath))
            {
                var content = Preprocessor.Process(fileStreamFactory, msBuildNuGetProjectSystem);

                try
                {
                    using (var transformation = new XmlTransformation(content, isTransformAFile: false, logger: null))
                    {
                        using (var document = new XmlTransformableDocument())
                        {
                            document.PreserveWhitespace = true;

                            // make sure we close the input stream immediately so that we can override 
                            // the file below when we save to it.
                            using (var inputStream = File.OpenRead(FileSystemUtility.GetFullPath(msBuildNuGetProjectSystem.ProjectFullPath, targetPath)))
                            {
                                document.Load(inputStream);
                            }

                            var succeeded = transformation.Apply(document);
                            if (succeeded)
                            {
                                // save the result into a memoryStream first so that if there is any
                                // exception during document.Save(), the original file won't be truncated.
                                MSBuildNuGetProjectSystemUtility.AddFile(msBuildNuGetProjectSystem, targetPath, document.Save);
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    throw new InvalidDataException(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.XdtError + " " + exception.Message,
                            targetPath,
                            msBuildNuGetProjectSystem.ProjectName),
                        exception);
                }
            }
        }
    }
}
