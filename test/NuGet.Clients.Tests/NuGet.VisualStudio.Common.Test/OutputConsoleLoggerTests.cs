// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using FluentAssertions;
using Moq;
using Xunit;

namespace NuGet.VisualStudio.Common.Test
{
    public class OutputConsoleLoggerTests : TestsRequiringJoinableTaskFactoryBase
    {
        [Fact]
        public void When_null_visualStudioShell_is_passed_ArgumentNullException_is_thrown()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new OutputConsoleLogger(visualStudioShell: null, new Mock<IOutputConsoleProvider>().Object, new Lazy<ErrorListTableDataSource>()));
            exception.ParamName.Should().Be("visualStudioShell");
        }

        [Fact]
        public void When_null_consoleProvider_is_passed_ArgumentNullException_is_thrown()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new OutputConsoleLogger(new Mock<IVisualStudioShell>().Object, consoleProvider: null, new Lazy<ErrorListTableDataSource>()));
            exception.ParamName.Should().Be("consoleProvider");
        }

        [Fact]
        public void When_null_errorListDataSource_is_passed_ArgumentNullException_is_thrown()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new OutputConsoleLogger(new Mock<IVisualStudioShell>().Object, new Mock<IOutputConsoleProvider>().Object, errorListDataSource: null));
            exception.ParamName.Should().Be("errorListDataSource");
        }
    }
}
