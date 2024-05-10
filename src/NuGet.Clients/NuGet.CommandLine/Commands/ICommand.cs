using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace NuGet.CommandLine
{
    [InheritedExport]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public interface ICommand
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        string CurrentDirectory { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        CommandAttribute CommandAttribute { get; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        DeprecatedCommandAttribute DeprecatedCommandAttribute { get; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        IList<string> Arguments { get; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        void Execute();
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        /// <summary>
        /// Returns a value indicating whether the specified option should be included in 
        /// the output of the help command.
        /// </summary>
        /// <param name="optionName">The name of the option.</param>
        /// <returns>True if the option should be included in the output of the help command;
        /// otherwise, false.</returns>
        bool IncludedInHelp(string optionName);
    }
}
