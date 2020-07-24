// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;

namespace NuGet.VisualStudio.Common.Test
{
    public class OutputConsoleLoggerTests
    {
        public class Constructor : TestsRequiringJoinableTaskFactoryBase
        {
            [Fact]
            public void When_null_visualStudioShell_is_passed_ArgumentNullException_is_thrown()
            {
                var exception = Assert.Throws<ArgumentNullException>(() => new OutputConsoleLogger(visualStudioShell: null, new Mock<IOutputConsoleProvider>().Object, new Lazy<INuGetErrorList>()));
                exception.ParamName.Should().Be("visualStudioShell");
            }

            [Fact]
            public void When_null_consoleProvider_is_passed_ArgumentNullException_is_thrown()
            {
                var exception = Assert.Throws<ArgumentNullException>(() => new OutputConsoleLogger(new Mock<IVisualStudioShell>().Object, consoleProvider: null, new Lazy<INuGetErrorList>()));
                exception.ParamName.Should().Be("consoleProvider");
            }

            [Fact]
            public void When_null_errorListDataSource_is_passed_ArgumentNullException_is_thrown()
            {
                var exception = Assert.Throws<ArgumentNullException>(() => new OutputConsoleLogger(new Mock<IVisualStudioShell>().Object, new Mock<IOutputConsoleProvider>().Object, errorList: null));
                exception.ParamName.Should().Be("errorList");
            }

            [Fact]
            public void Subscribes_to_BuildBegin_events()
            {
                var visualStudioShell = new Mock<IVisualStudioShell>();

                var logger = new OutputConsoleLogger(visualStudioShell.Object, new Mock<IOutputConsoleProvider>().Object, new Lazy<INuGetErrorList>());

                visualStudioShell.Verify(vss => vss.SubscribeToBuildBeginAsync(It.IsAny<Action>()), Times.Exactly(1));
            }

            [Fact]
            public void BuildBegin_event_clears_error_list_entries()
            {
                Action onBuildBegin = null;

                var visualStudioShell = new Mock<IVisualStudioShell>();
                visualStudioShell.Setup(vss => vss.SubscribeToBuildBeginAsync(It.IsAny<Action>()))
                                 .Returns(Task.CompletedTask)
                                 .Callback((Action action) => { onBuildBegin = action; });

                var errorList = new Mock<INuGetErrorList>();

                var logger = new OutputConsoleLogger(visualStudioShell.Object, new Mock<IOutputConsoleProvider>().Object, new Lazy<INuGetErrorList>(() => errorList.Object));

                onBuildBegin();
                errorList.Verify(eltds => eltds.ClearNuGetEntries(), Times.Exactly(1));
            }

            [Fact]
            public void Subscribes_to_AfterClosing_events()
            {
                var visualStudioShell = new Mock<IVisualStudioShell>();

                var logger = new OutputConsoleLogger(visualStudioShell.Object, new Mock<IOutputConsoleProvider>().Object, new Lazy<INuGetErrorList>());

                visualStudioShell.Verify(vss => vss.SubscribeToAfterClosingAsync(It.IsAny<Action>()), Times.Exactly(1));
            }

            [Fact]
            public void AfterClosing_event_clears_error_list_entries()
            {
                Action afterClosing = null;

                var visualStudioShell = new Mock<IVisualStudioShell>();
                visualStudioShell.Setup(vss => vss.SubscribeToAfterClosingAsync(It.IsAny<Action>()))
                                 .Returns(Task.CompletedTask)
                                 .Callback((Action action) => { afterClosing = action; });

                var errorList = new Mock<INuGetErrorList>();

                var logger = new OutputConsoleLogger(visualStudioShell.Object, new Mock<IOutputConsoleProvider>().Object, new Lazy<INuGetErrorList>(() => errorList.Object));

                afterClosing();
                errorList.Verify(eltds => eltds.ClearNuGetEntries(), Times.Exactly(1));
            }

            [Fact]
            public void Creates_package_manager_console()
            {
                var outputConsoleProvider = new Mock<IOutputConsoleProvider>();

                var logger = new OutputConsoleLogger(new Mock<IVisualStudioShell>().Object, outputConsoleProvider.Object, new Lazy<INuGetErrorList>());

                outputConsoleProvider.Verify(ocp => ocp.CreatePackageManagerConsoleAsync(), Times.Exactly(1));
            }
        }
    }
}
