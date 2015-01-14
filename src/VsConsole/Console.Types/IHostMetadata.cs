namespace NuGetConsole
{
    /// <summary>
    /// Host MEF metadata viewer.
    /// </summary>
    public interface IHostMetadata
    {
        /// <summary>
        /// Get the HostName MEF metadata.
        /// </summary>
        string HostName { get; }

        /// <summary>
        /// Get the DisplayName MEF metadata.
        /// </summary>
        string DisplayName { get; }
    }
}
