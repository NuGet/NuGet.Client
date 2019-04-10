// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;

namespace NuGet.CommandLine
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class DeprecatedCommandAttribute : Attribute
    {
        public Type AlternativeCommand { get; set; }

        public DeprecatedCommandAttribute(Type alternativeCommand)
        {
            AlternativeCommand = alternativeCommand;
        }


        public string GetDeprecationMessage(string currentCommand)
        {
            var cmdAttrs = AlternativeCommand.GetCustomAttributes(typeof(CommandAttribute), false);
            var cmdAttr = cmdAttrs.FirstOrDefault() as CommandAttribute;

            var binaryName = Assembly.GetExecutingAssembly().GetName().Name;

            // TODO: Use string resource
            return string.Format("'{0} {1}' is deprecated. Use '{0} {2}' instead", binaryName, currentCommand, cmdAttr.CommandName);
        }



        public string DeprecatedWord()
        {
            // TODO: Use string resource
            return "DEPRECATED";
        }
    }
}
