namespace NuGet.Packaging.Core
{
    public static class PackagingCoreConstants
    {
        public const string NupkgExtension = ".nupkg";
        public const string NuspecExtension = ".nuspec";

        /// <summary>
        /// _._ denotes an empty folder since OPC does not allow an
        /// actual empty folder.
        /// </summary>
        public static readonly string EmptyFolder = "_._";

        /// <summary>
        /// /_._ can be used to check empty folders from package readers where the / is normalized.
        /// </summary>
        public static readonly string ForwardSlashEmptyFolder = "/" + EmptyFolder;
    }
}
