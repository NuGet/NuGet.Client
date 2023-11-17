// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Moq;
using NuGet.CommandLine.XPlat;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class PackageSearchTestInitializer
    {
        internal CommandLineApplication App { get; set; }
        internal Func<ILoggerWithColor> GetLogger { get; set; }
        internal PackageSearchArgs CapturedArgs { get; set; }
        internal Func<PackageSearchArgs, string, Task<int>> SetupSettingsAndRunSearchAsync { get; set; }
        internal string StoredErrorMessage { get; set; }
        internal List<Tuple<string, ConsoleColor>> ColoredMessage { get; set; }
        internal string Message { get; set; }

        public PackageSearchTestInitializer()
        {
            StoredErrorMessage = string.Empty;
            ColoredMessage = new List<Tuple<string, ConsoleColor>>();
            App = new CommandLineApplication();
            var loggerWithColorMock = new Mock<ILoggerWithColor>();
            loggerWithColorMock.Setup(x => x.LogError(It.IsAny<string>()))
                .Callback<string>(message => StoredErrorMessage += message);

            loggerWithColorMock.Setup(x => x.LogMinimal(It.IsAny<string>(), It.IsAny<ConsoleColor>()))
                .Callback<string, ConsoleColor>((message, color) => { ColoredMessage.Add(Tuple.Create(message, color)); });

            loggerWithColorMock.Setup(x => x.LogMinimal(It.IsAny<string>()))
                .Callback<string>((message) => { Message += message + "\n"; });

            GetLogger = () => loggerWithColorMock.Object;

            CapturedArgs = null;

            SetupSettingsAndRunSearchAsync = async (PackageSearchArgs args, string configFile) =>
            {
                CapturedArgs = args;
                await Task.CompletedTask;
                return 0;
            };
        }
    }
}
