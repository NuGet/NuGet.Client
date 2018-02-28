namespace NuGet.Tests.Foundation.Utility.IO
{
    /// <summary>
    /// The various path formats.
    /// </summary>
    public enum PathFormat
    {
        /// <summary>
        /// Unknown (and possibly invalid) format
        /// </summary>
        UnknownFormat,

        /// <summary>
        /// Fully qualified against a specific drive. (C:\rest_of_path)
        /// </summary>
        DriveAbsolute,

        /// <summary>
        /// Extended length format. (\\?\C:\rest_of_path)
        /// </summary>
        DriveAbsoluteExtended,

        /// <summary>
        /// Relative to the current directory on the specified drive. (C:rest_of_path)
        /// </summary>
        DriveRelative,

        /// <summary>
        /// Rooted to the current drive. (\rest_of_path)
        /// </summary>
        Rooted,

        /// <summary>
        /// UNC (\\server\share)
        /// </summary>
        UniformNamingConvention,

        /// <summary>
        /// UNC extended length format (\\?\UNC\Server\Share)
        /// </summary>
        UniformNamingConventionExtended,

        /// <summary>
        /// Relative to the current working directory (rest_of_path)
        /// </summary>
        Relative
    }
}
