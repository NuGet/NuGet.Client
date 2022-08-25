// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.VisualStudio.Experimentation;
using Moq;
using NuGet.VisualStudio;

namespace Test.Utility.VisualStudio
{
    public static class NuGetExperimentationServiceUtility
    {
        public static IExperimentationService GetMock(Dictionary<string, bool>? flights = null)
        {
            var mock = new Mock<IExperimentationService>();
            var d = mock.Setup(m => m.IsCachedFlightEnabled(It.IsAny<string>()));

            if (flights is null)
            {
                d.Returns(false);
            }
            else
            {
                d.Returns((string s) => flights.ContainsKey(s) && flights[s]);
            }

            return mock.Object;
        }
    }

    public class TestOutputConsoleProvider : IOutputConsoleProvider
    {
        public TestOutputConsole TestOutputConsole { get; } = new TestOutputConsole();

        public Task<IOutputConsole> CreateBuildOutputConsoleAsync()
        {
            throw new NotImplementedException();
        }

        public Task<IOutputConsole> CreatePackageManagerConsoleAsync()
        {
            return Task.FromResult((IOutputConsole)TestOutputConsole);
        }

        public Task<IConsole> CreatePowerShellConsoleAsync()
        {
            throw new NotImplementedException();
        }
    }

    public class TestOutputConsole : IOutputConsole
    {
        public IList<string> Messages { get; } = new List<string>();

        public int ConsoleWidth => throw new NotImplementedException();

        public Task ActivateAsync()
        {
            throw new NotImplementedException();
        }

        public Task ClearAsync()
        {
            throw new NotImplementedException();
        }

        public Task WriteAsync(string text)
        {
            throw new NotImplementedException();
        }

        public Task WriteAsync(string text, Color? foreground, Color? background)
        {
            throw new NotImplementedException();
        }

        public Task WriteBackspaceAsync()
        {
            throw new NotImplementedException();
        }

        public Task WriteLineAsync(string text)
        {
            Messages.Add(text);
            return Task.CompletedTask;
        }

        public Task WriteLineAsync(string format, params object[] args)
        {
            throw new NotImplementedException();
        }

        public Task WriteProgressAsync(string currentOperation, int percentComplete)
        {
            throw new NotImplementedException();
        }
    }

}
