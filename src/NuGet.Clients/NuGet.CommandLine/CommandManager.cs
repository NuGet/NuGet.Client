// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NuGet.Commands;

namespace NuGet.CommandLine
{
    [Export(typeof(ICommandManager))]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class CommandManager : ICommandManager
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        private readonly IList<ICommand> _commands = new List<ICommand>();

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public IEnumerable<ICommand> GetCommands()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            return _commands;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public ICommand GetCommand(string commandName)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            IEnumerable<ICommand> results = from command in _commands
                                            where command.CommandAttribute.CommandName.StartsWith(commandName, StringComparison.OrdinalIgnoreCase) ||
                                            (command.CommandAttribute.AltName ?? String.Empty).StartsWith(commandName, StringComparison.OrdinalIgnoreCase)
                                            select command;

            if (!results.Any())
            {
                throw new CommandException(LocalizedResourceManager.GetString("UnknowCommandError"), commandName);
            }

            var matchedCommand = results.First();
            if (results.Skip(1).Any())
            {
                // Were there more than one results found?
                matchedCommand = results.FirstOrDefault(c => c.CommandAttribute.CommandName.Equals(commandName, StringComparison.OrdinalIgnoreCase)
                    || commandName.Equals(c.CommandAttribute.AltName, StringComparison.OrdinalIgnoreCase));

                if (matchedCommand == null)
                {
                    // No exact match was found and the result returned multiple prefixes.
                    throw new CommandException(String.Format(CultureInfo.CurrentCulture, LocalizedResourceManager.GetString("AmbiguousCommand"), commandName,
                        String.Join(" ", from c in results select c.CommandAttribute.CommandName)));
                }
            }
            return matchedCommand;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public IDictionary<OptionAttribute, PropertyInfo> GetCommandOptions(ICommand command)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            var result = new Dictionary<OptionAttribute, PropertyInfo>();

            foreach (PropertyInfo propInfo in command.GetType().GetProperties())
            {
                if (!command.IncludedInHelp(propInfo.Name))
                {
                    continue;
                }

                foreach (OptionAttribute attr in propInfo.GetCustomAttributes(typeof(OptionAttribute), inherit: true))
                {
                    if (!propInfo.CanWrite && !TypeHelper.IsMultiValuedProperty(propInfo))
                    {
                        // If the property has neither a setter nor is of a type that can be cast to ICollection<> then there's no way to assign 
                        // values to it. In this case throw.
                        throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                            LocalizedResourceManager.GetString("OptionInvalidWithoutSetter"), command.GetType().FullName + "." + propInfo.Name));
                    }
                    result.Add(attr, propInfo);
                }
            }

            return result;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public void RegisterCommand(ICommand command)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            var attrib = command.CommandAttribute;
            if (attrib != null)
            {
                _commands.Add(command);
            }
        }
    }
}
