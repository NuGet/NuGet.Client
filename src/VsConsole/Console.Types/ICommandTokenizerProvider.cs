namespace NuGetConsole
{
    /// <summary>
    /// ICommandTokenizer provider (syntax highlighting).
    /// 
    /// To support syntax highlighting, export an implementation of this interface
    /// and apply a HostName attribute to associate it with the host.
    /// </summary>
    public interface ICommandTokenizerProvider
    {
        /// <summary>
        /// Create a command line tokenizer for a host.
        /// </summary>
        /// <param name="host">The host instance to apply the command tokenizer.</param>
        /// <returns>A command line tokenizer.</returns>
        ICommandTokenizer Create(IHost host);
    }
}
