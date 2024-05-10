using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NuGet.CommandLine
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public interface ICommandManager
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Method would do reflection and a property would be inappropriate.")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        IEnumerable<ICommand> GetCommands();
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        ICommand GetCommand(string commandName);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        IDictionary<OptionAttribute, PropertyInfo> GetCommandOptions(ICommand command);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        void RegisterCommand(ICommand command);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
