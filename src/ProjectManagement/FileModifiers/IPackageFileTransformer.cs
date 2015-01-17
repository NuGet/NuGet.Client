using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
