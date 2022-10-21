// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Commands.Test
{
    public class CLILanguageOverriderTests
    {
        private readonly TestLogger _logger;

        public CLILanguageOverriderTests(ITestOutputHelper output)
        {
            _logger = new TestLogger(output);
        }

        [Fact]
        public void Constructor_NullArguments_Throws()
        {
            // Arrange, Act and Assert
            Assert.Throws<ArgumentNullException>(() => new CLILanguageOverrider(logger: null, It.IsAny<IEnumerable<LanguageEnvironmentVariable>>(), It.IsAny<bool>()));
            Assert.Throws<ArgumentNullException>(() => new CLILanguageOverrider(logger: It.IsAny<ILogger>(), varsToProbe: null, It.IsAny<bool>()));
        }

        [Theory]
        [InlineData("fr-FR")]
        public void SetUp_WithNewVariableAndFlowToProcessFlag_SetsValueToOtherVars(string overrideCultureInfo)
        {
            // Arrange
            var envvar1 = new LanguageEnvironmentVariable("LANG_VAR_TEST_FLOW_1", LanguageEnvironmentVariable.GetCultureFromName, LanguageEnvironmentVariable.CultureToName);
            var envvar2 = new LanguageEnvironmentVariable("LANG_VAR_TEST_FLOW_2", LanguageEnvironmentVariable.GetCultureFromName, LanguageEnvironmentVariable.CultureToName);
            var cliLang = new CLILanguageOverrider(_logger, new[] { envvar1, envvar2 }, flowEnvvarsToChildProcess: true);

            Environment.SetEnvironmentVariable(envvar1.VariableName, overrideCultureInfo, EnvironmentVariableTarget.Process);

            // Act
            cliLang.Setup();

            // Assert
            Assert.Equal(overrideCultureInfo, Environment.GetEnvironmentVariable(envvar2.VariableName));

            // Cleanup
            Environment.SetEnvironmentVariable(envvar1.VariableName, null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(envvar2.VariableName, null, EnvironmentVariableTarget.Process);
        }

        [Fact]
        public void SetUp_WithNewVariableAndWithoutFlowToProcessFlag_ChildEnvVarsAreNotSet()
        {
            // Arrange
            var envvar1 = new LanguageEnvironmentVariable("LANG_VAR_TEST_NOTSET_1", LanguageEnvironmentVariable.GetCultureFromName, LanguageEnvironmentVariable.CultureToName);
            var envvar2 = new LanguageEnvironmentVariable("LANG_VAR_TEST_NOTSET_2", LanguageEnvironmentVariable.GetCultureFromName, LanguageEnvironmentVariable.CultureToName);
            var cliLang = new CLILanguageOverrider(_logger, new[] { envvar1, envvar2 }, flowEnvvarsToChildProcess: false);
            var overrideCultureInfo = "fr-FR";

            Environment.SetEnvironmentVariable(envvar1.VariableName, overrideCultureInfo, EnvironmentVariableTarget.Process);

            // Act
            cliLang.Setup();

            // Assert
            Assert.Null(Environment.GetEnvironmentVariable(envvar2.VariableName));

            // Cleanup
            Environment.SetEnvironmentVariable(envvar1.VariableName, null, EnvironmentVariableTarget.Process);
        }

        [Theory]
        [InlineData(" ")]
        [InlineData("")]
        public void SetUp_WithEmptyEnvvarsAndFlowToProcessFlag_ChildEnvVarsAreNotSet(string overrideCultureInfo)
        {
            // Arrange
            var envvar1 = new LanguageEnvironmentVariable("LANG_VAR_TEST_NOTSET_1", LanguageEnvironmentVariable.GetCultureFromName, LanguageEnvironmentVariable.CultureToName);
            var envvar2 = new LanguageEnvironmentVariable("LANG_VAR_TEST_NOTSET_2", LanguageEnvironmentVariable.GetCultureFromName, LanguageEnvironmentVariable.CultureToName);
            var cliLang = new CLILanguageOverrider(_logger, new[] { envvar1, envvar2 }, flowEnvvarsToChildProcess: true);

            Environment.SetEnvironmentVariable(envvar1.VariableName, overrideCultureInfo, EnvironmentVariableTarget.Process);

            // Act
            cliLang.Setup();

            // Assert
            Assert.Null(Environment.GetEnvironmentVariable(envvar2.VariableName));

            // Cleanup
            Environment.SetEnvironmentVariable(envvar1.VariableName, null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(envvar2.VariableName, null, EnvironmentVariableTarget.Process);
        }
    }
}
