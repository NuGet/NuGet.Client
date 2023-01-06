// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGet.VisualStudio.Common.Test
{
    public partial class OutputConsoleLoggerTests
    {
        public class Start : OutputConsoleLoggerTests, IAsyncLifetime
        {
            public Start()
            { }

            public async Task InitializeAsync()
            {
                _outputConsole.Reset();
                _outputConsoleLogger.Start();
                await EnsureInitialized();
            }

            [Fact]
            public void Activates_output_console()
            {
                _outputConsole.Verify(oc => oc.ActivateAsync());
            }

            [Fact]
            public void Clears_output_console()
            {
                _outputConsole.Verify(oc => oc.ClearAsync());
            }

            [Fact]
            public void Clears_error_list()
            {
                _errorList.Verify(el => el.ClearNuGetEntries());
            }
        }
    }
}
