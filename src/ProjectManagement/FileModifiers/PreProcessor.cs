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
        public void TransformFile(ZipArchiveEntry packageFile, string targetPath, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            MSBuildNuGetProjectSystemUtility.TryAddFile(msBuildNuGetProjectSystem, targetPath,
                () => StreamUtility.StreamFromString(Process(packageFile, msBuildNuGetProjectSystem)));
        }

        public void RevertFile(ZipArchiveEntry packageFile, string targetPath, IEnumerable<InternalZipFileInfo> matchingFiles, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            MSBuildNuGetProjectSystemUtility.DeleteFileSafe(targetPath,
                () => StreamUtility.StreamFromString(Process(packageFile, msBuildNuGetProjectSystem)),
                msBuildNuGetProjectSystem);
        }

        internal static string Process(ZipArchiveEntry packageFile, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem)
        {
            using (var stream = packageFile.Open())
            {
                return Process(stream, msBuildNuGetProjectSystem, throwIfNotFound: false);
            }
        }

        public static string Process(Stream stream, IMSBuildNuGetProjectSystem msBuildNuGetProjectSystem, bool throwIfNotFound = true)
        {
            string text;
            using(StreamReader streamReader = new StreamReader(stream))
            {
                text = streamReader.ReadToEnd();
            }
            var tokenizer = new Tokenizer(text);
            StringBuilder result = new StringBuilder();
            for (; ; )
            {
                Token token = tokenizer.Read();
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
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Strings.TokenHasNoValue, propertyName));
            }
            return value;
        }
    }
}
