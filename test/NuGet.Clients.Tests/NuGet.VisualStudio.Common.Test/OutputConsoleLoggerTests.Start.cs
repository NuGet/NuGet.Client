// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using Xunit;

namespace NuGet.VisualStudio.Common.Test
{
    public partial class OutputConsoleLoggerTests
    {
        public class Start : OutputConsoleLoggerTests
        {
            public Start()
            {
                _outputConsole.Reset();
                _outputConsoleLogger.Start();
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
            public void Gets_MSBuild_verbosity_from_shell()
            {
                _visualStudioShell.Verify(vss => vss.GetPropertyValueAsync("Environment", "ProjectsAndSolution", "MSBuildOutputVerbosity"));
            }

            [Fact]
            public void Clears_error_list()
            {
                _errorList.Verify(el => el.ClearNuGetEntries());
            }
        }
    }
}
