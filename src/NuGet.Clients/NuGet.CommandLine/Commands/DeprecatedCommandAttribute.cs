// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;

namespace NuGet.CommandLine
{
    /// <summary>
    /// Attribute for marking nuget.exe commands as deprecated.
    ///
    /// Using this attribute will in ICommand--based classes, it will show a warning message each
    /// command invocation and in each help command invocation.
    ///
    /// You need to provide an alternative command to use this attribute
    ///
    /// 
    /// <see cref="ICommand"/>
    /// <see cref="Command"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class DeprecatedCommandAttribute : Attribute
    {
        public Type AlternativeCommand { get; set; }

        public DeprecatedCommandAttribute(Type alternativeCommand)
        {
            if (alternativeCommand == null || !typeof(ICommand).IsAssignableFrom(alternativeCommand))
            {
                throw new ArgumentException("alternativeCommand must be a Type that implements ICommand interface");
            }
            AlternativeCommand = alternativeCommand;
        }


        public string GetDeprecationMessage(string currentCommand)
        {
            var cmdAttrs = AlternativeCommand.GetCustomAttributes(typeof(CommandAttribute), false);
            var cmdAttr = cmdAttrs.FirstOrDefault() as CommandAttribute;

            var binaryName = Assembly.GetExecutingAssembly().GetName().Name;

            var warningResource = LocalizedResourceManager.GetString("CommandDeprecationWarning");

            return string.Format(warningResource, binaryName, currentCommand, cmdAttr.CommandName);
        }


        public string DeprecatedWord()
        {
            return LocalizedResourceManager.GetString("DeprecatedWord");
        }
    }
}
