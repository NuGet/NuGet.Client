// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Moq;
using NuGet.CommandLine.XPlat;
using static NuGet.CommandLine.XPlat.PackageSearchCommand;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class PackageSearchTestInitializer
    {
        internal CommandLineApplication App { get; set; }
        internal Func<ILoggerWithColor> GetLogger { get; set; }
        internal PackageSearchArgs CapturedArgs { get; set; }
        internal SetupSettingsAndRunSearchAsyncDelegate SetupSettingsAndRunSearchAsyncDelegate { get; set; }
        internal string StoredErrorMessage { get; set; }
        internal List<Tuple<string, ConsoleColor>> ColoredMessage { get; set; }

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
            GetLogger = () => loggerWithColorMock.Object;

            CapturedArgs = null;

            SetupSettingsAndRunSearchAsyncDelegate = async (PackageSearchArgs args) =>
            {
                CapturedArgs = args;
                await Task.CompletedTask;
                return 0;
            };
        }
    }
}
