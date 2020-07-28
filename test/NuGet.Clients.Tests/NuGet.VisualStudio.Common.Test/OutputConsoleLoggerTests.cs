// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;

namespace NuGet.VisualStudio.Common.Test
{
    [Collection(nameof(TestJoinableTaskFactoryCollection))]
    public abstract class OutputConsoleLoggerTests
    {
        private protected Action _onBuildBegin;
        private protected Action _afterClosing;
        private protected int _msBuildOutputVerbosity;

        private protected readonly Mock<IVisualStudioShell> _visualStudioShell = new Mock<IVisualStudioShell>();
        private protected readonly Mock<IOutputConsoleProvider> _outputConsoleProvider = new Mock<IOutputConsoleProvider>();
        private protected readonly Mock<IOutputConsole> _outputConsole = new Mock<IOutputConsole>();
        private protected readonly Mock<INuGetErrorList> _errorList = new Mock<INuGetErrorList>();
        private protected readonly OutputConsoleLogger _outputConsoleLogger;

        protected OutputConsoleLoggerTests()
        {
            _visualStudioShell.Setup(vss => vss.SubscribeToBuildBeginAsync(It.IsAny<Action>()))
                              .Returns(Task.CompletedTask)
                              .Callback((Action action) => { _onBuildBegin = action; });

            _visualStudioShell.Setup(vss => vss.SubscribeToAfterClosingAsync(It.IsAny<Action>()))
                              .Returns(Task.CompletedTask)
                              .Callback((Action action) => { _afterClosing = action; });

            _visualStudioShell.Setup(vss => vss.GetPropertyValueAsync("Environment", "ProjectsAndSolution", "MSBuildOutputVerbosity"))
                              .Returns(Task.FromResult((object)_msBuildOutputVerbosity));

            _outputConsoleProvider.Setup(ocp => ocp.CreatePackageManagerConsoleAsync())
                                  .Returns(Task.FromResult(_outputConsole.Object));

            _outputConsoleLogger = new OutputConsoleLogger(_visualStudioShell.Object, _outputConsoleProvider.Object, new Lazy<INuGetErrorList>(() => _errorList.Object));
        }

        public class Constructor : OutputConsoleLoggerTests
        {
            [Fact]
            public void When_null_visualStudioShell_is_passed_ArgumentNullException_is_thrown()
            {
                var exception = Assert.Throws<ArgumentNullException>(() => new OutputConsoleLogger(visualStudioShell: null, _outputConsoleProvider.Object, new Lazy<INuGetErrorList>(() => _errorList.Object)));
                exception.ParamName.Should().Be("visualStudioShell");
            }

            [Fact]
            public void When_null_consoleProvider_is_passed_ArgumentNullException_is_thrown()
            {
                var exception = Assert.Throws<ArgumentNullException>(() => new OutputConsoleLogger(_visualStudioShell.Object, consoleProvider: null, new Lazy<INuGetErrorList>(() => _errorList.Object)));
                exception.ParamName.Should().Be("consoleProvider");
            }

            [Fact]
            public void When_null_errorListDataSource_is_passed_ArgumentNullException_is_thrown()
            {
                var exception = Assert.Throws<ArgumentNullException>(() => new OutputConsoleLogger(_visualStudioShell.Object, _outputConsoleProvider.Object, errorList: null));
                exception.ParamName.Should().Be("errorList");
            }

            [Fact]
            public void Subscribes_to_BuildBegin_events()
            {
                _visualStudioShell.Verify(vss => vss.SubscribeToBuildBeginAsync(It.IsAny<Action>()), Times.Exactly(1));
            }

            [Fact]
            public void BuildBegin_event_clears_error_list_entries()
            {
                _onBuildBegin();
                _errorList.Verify(el => el.ClearNuGetEntries(), Times.Exactly(1));
            }

            [Fact]
            public void Subscribes_to_AfterClosing_events()
            {
                _visualStudioShell.Verify(vss => vss.SubscribeToAfterClosingAsync(It.IsAny<Action>()), Times.Exactly(1));
            }

            [Fact]
            public void AfterClosing_event_clears_error_list_entries()
            {
                _afterClosing();
                _errorList.Verify(eltds => eltds.ClearNuGetEntries(), Times.Exactly(1));
            }

            [Fact]
            public void Creates_package_manager_console()
            {
                _outputConsoleProvider.Verify(ocp => ocp.CreatePackageManagerConsoleAsync(), Times.Exactly(1));
            }
        }

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
