// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.VisualStudio.Experimentation;
using NuGet.VisualStudio;

namespace Test.Utility.VisualStudio
{
    public static class NuGetExperimentationServiceUtility
    {
        ////public static Mock<INuGetExperimentationService> GetMock()
        //{
        //    var mock = new Mock<INuGetExperimentationService>();
        //    return mock;
        //}
    }

    public class TestVisualStudioExperimentalService : IExperimentationService
    {
        private readonly Dictionary<string, bool> _flights;

        public TestVisualStudioExperimentalService()
            : this(new Dictionary<string, bool>())
        {
        }

        public TestVisualStudioExperimentalService(Dictionary<string, bool> flights)
        {
            _flights = flights ?? throw new ArgumentNullException(nameof(flights));
        }

        public void Dispose()
        {
            // do nothing
        }

        public bool IsCachedFlightEnabled(string flight)
        {
            _flights.TryGetValue(flight, out bool result);
            return result;
        }

        public Task<bool> IsFlightEnabledAsync(string flight, CancellationToken token)
        {
            return Task.FromResult(IsCachedFlightEnabled(flight));
        }

        public void Start()
        {
            // do nothing.
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
