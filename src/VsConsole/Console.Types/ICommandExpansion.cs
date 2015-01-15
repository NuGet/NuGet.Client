namespace NuGetConsole
{
    /// <summary>
    /// Interface for command line expansion (intellisense).
    /// </summary>
    public interface ICommandExpansion
    {
        /// <summary>
        /// Get command line expansion candidates.
        /// </summary>
        /// <param name="line">The current input line content.</param>
        /// <param name="caretIndex">The caret position in the input line (starting from 0).</param>
        /// <returns>Command line expansion result.</returns>
        SimpleExpansion GetExpansions(string line, int caretIndex);
    }
}
