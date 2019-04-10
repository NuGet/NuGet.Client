// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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


        public string GetDeprecationMessage(string binaryName, string currentCommand)
        {
            var cmdAttrs = AlternativeCommand.GetCustomAttributes(typeof(CommandAttribute), false);
            var cmdAttr = cmdAttrs.FirstOrDefault() as CommandAttribute;

            // TODO: Use string resource
            return string.Format("'{0} {1}' is deprecated. Use '{0} {2}' instead", binaryName, currentCommand, cmdAttr.CommandName);
        }


        public string GetDeprepateWord()
        {
            // TODO: Use string resource
            return "DEPRECATED";
        }
    }
}
