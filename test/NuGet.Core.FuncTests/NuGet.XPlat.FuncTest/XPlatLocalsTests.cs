// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.CommandLine.XPlat;
using System;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class XPlatLocalsTests
    {
        [Theory]
        [InlineData("locals --list all")]
        [InlineData("locals -l all")]
        public static void Locals_List_Succeeds(String args)
        {
            var log = new TestCommandOutputLogger();
            var exitCode = Program.MainInternal(args.Split(null), log);
            Assert.Equal(string.Empty, log.ShowErrors());
            Assert.Equal(0, exitCode);
        }

        [Theory]
        [InlineData("locals")]
        [InlineData("locals --list")]
        [InlineData("locals -l")]
        [InlineData("locals --clear")]
        [InlineData("locals -c")]
        public static void Locals_Success_InvalidArguments_HelpMessage(String args)
        {
            var log = new TestCommandOutputLogger();
            var exitCode = Program.MainInternal(args.Split(null), log);
            Assert.Equal("usage: NuGet locals <all | http-cache | global-packages | temp> [-clear | -list]"+ Environment.NewLine + "For more information, visit http://docs.nuget.org/docs/reference/command-line-reference", log.ShowErrors());
            Assert.Equal(1, exitCode);
        }

        [Theory]
        [InlineData("locals --list unknownResource")]
        [InlineData("locals -l unknownResource")]
        [InlineData("locals --clear unknownResource")]
        [InlineData("locals -c unknownResource")]
        public static void Locals_Success_InvalidResourceName_HelpMessage(String args)
        {
            var log = new TestCommandOutputLogger();
            var exitCode = Program.MainInternal(args.Split(null), log);
            Assert.Equal("An invalid local resource name was provided. Please provide one of the following values: http-cache, temp, global-packages, all.", log.ShowErrors());
            Assert.Equal(1, exitCode);
        }
    }   
}
