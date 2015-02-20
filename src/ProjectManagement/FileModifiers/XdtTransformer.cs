using Microsoft.Web.XmlTransform;
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace NuGet.ProjectManagement
{
    public class XdtTransformer : IPackageFileTransformer
    {
        public void TransformFile(ZipArchiveEntry packageFile, string targetPath, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            PerformXdtTransform(packageFile, targetPath, msBuildNuGetProjectSystem);
        }

        public void RevertFile(ZipArchiveEntry packageFile, string targetPath, System.Collections.Generic.IEnumerable<InternalZipFileInfo> matchingFiles, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            PerformXdtTransform(packageFile, targetPath, msBuildNuGetProjectSystem);
        }

        private static void PerformXdtTransform(ZipArchiveEntry packageFile, string targetPath, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            if(FileSystemUtility.FileExists(msBuildNuGetProjectSystem.ProjectFullPath, targetPath))
            {
                string content = Preprocessor.Process(packageFile, msBuildNuGetProjectSystem);

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

                            bool succeeded = transformation.Apply(document);
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
                        String.Format(
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
