// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.CommandLine.XPlat;
using System;
using System.Collections.Generic;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class XPlatLocalsTests
    {
        [Fact]
        public static void Locals_List_Succeeds()
        {
            var log = new TestCommandOutputLogger();
            var args = new List<string>()
            {
                "locals",
                "--list",
                "all"
            };
            var exitCode = Program.MainInternal(args.ToArray(), log);
            Assert.Equal(string.Empty, log.ShowErrors());
            Assert.Equal(0, exitCode);
        }

        [Fact]
        public static void Locals_Success_InvalidArguments_HelpMessage()
        {
            var log = new TestCommandOutputLogger();
            var args = new List<string>()
            {
                "locals",
                "--list"
            };
            var exitCode = Program.MainInternal(args.ToArray(), log);
            Assert.Equal("usage: NuGet locals < all | http - cache | global - packages | temp > [-clear | -list] \nFor more information, visit http://docs.nuget.org/docs/reference/command-line-reference", log.ShowErrors());
            Assert.Equal(1, exitCode);
        }
    }

   
}
