// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NuGet.Common;
using Xunit;

namespace NuGet.VisualStudio.Common.Test
{
    public partial class OutputConsoleLoggerTests
    {
        public class Log : OutputConsoleLoggerTests
        {
            [Fact]
            public void Gets_MSBuild_verbosity_from_shell()
            {
                _outputConsoleLogger.Log(new LogMessage(LogLevel.Debug, "message"));
                _visualStudioShell.Verify(vss => vss.GetPropertyValueAsync("Environment", "ProjectsAndSolution", "MSBuildOutputVerbosity"));
            }

            [Fact]
            public void Non_integer_verbosity_is_interpreted_as_2()
            {
                _outputConsole.Reset();
                _msBuildOutputVerbosity = "string";

                _outputConsoleLogger.Log(new LogMessage(LogLevel.Debug, "message"));

                _outputConsole.VerifyNoOtherCalls();
            }

            [Theory]
            [MemberData(nameof(GetMessageVariationsWhichAreLogged))]
            public void Writes_line_to_output_console(LogLevel logLevel, int verbosityLevel)
            {
                _outputConsole.Reset();
                _msBuildOutputVerbosity = verbosityLevel;

                _outputConsoleLogger.Log(new LogMessage(logLevel, "message"));

                _outputConsole.Verify(oc => oc.WriteLineAsync("message"));
            }

            public static IEnumerable<object[]> GetMessageVariationsWhichAreLogged()
            {
                return GetMessageVariations()
                      .Where(v => v.logged)
                      .Select(v => v.variation);
            }

            [Theory]
            [MemberData(nameof(GetMessageVariantsWhichAreNotLogged))]
            public void Does_not_write_line_to_output_console(LogLevel logLevel, int verbosityLevel)
            {
                _outputConsole.Reset();
                _msBuildOutputVerbosity = verbosityLevel;

                _outputConsoleLogger.Log(new LogMessage(logLevel, "message"));

                _outputConsole.VerifyNoOtherCalls();
            }

            public static IEnumerable<object[]> GetMessageVariantsWhichAreNotLogged()
            {
                return GetMessageVariations()
                      .Where(v => !v.logged)
                      .Select(v => v.variation);
            }

            [Theory]
            [MemberData(nameof(GetMessageVariationsWhichAreReported))]
            public void Adds_entry_to_error_list(LogLevel logLevel, int verbosityLevel)
            {
                ErrorListTableEntry[] errorListTableEntries = null;

                _errorList.Reset();
                _errorList.Setup(el => el.AddNuGetEntries(It.IsAny<ErrorListTableEntry[]>()))
                          .Callback<ErrorListTableEntry[]>(elte => errorListTableEntries = elte);

                _msBuildOutputVerbosity = verbosityLevel;

                _outputConsoleLogger.Log(new LogMessage(logLevel, "message"));

                _errorList.Verify(el => el.AddNuGetEntries(It.IsAny<ErrorListTableEntry[]>()));

                errorListTableEntries.Length.Should().Be(1);
                errorListTableEntries[0].Message.Message.Should().Be("message");
                errorListTableEntries[0].Message.Level.Should().Be(logLevel);
            }

            public static IEnumerable<object[]> GetMessageVariationsWhichAreReported()
            {
                return GetMessageVariations()
                      .Where(v => v.reported)
                      .Select(v => v.variation);
            }

            [Theory]
            [MemberData(nameof(GetMessageVariationsWhichAreNotReported))]
            public void Does_not_add_entry_to_error_list(LogLevel logLevel, int verbosityLevel)
            {
                _errorList.Reset();
                _msBuildOutputVerbosity = verbosityLevel;

                _outputConsoleLogger.Log(new LogMessage(logLevel, "message"));

                _errorList.VerifyNoOtherCalls();
            }

            public static IEnumerable<object[]> GetMessageVariationsWhichAreNotReported()
            {
                return GetMessageVariations()
                      .Where(v => !v.reported)
                      .Select(v => v.variation);
            }

            private static IEnumerable<(bool logged, bool reported, object[] variation)> GetMessageVariations()
            {
                foreach (var verbosityLevel in new int[] { 0, 1, 2, 3, 4 })
                {
                    // Every information message is logged, but not reported, regardless of verbosity level.
                    yield return (logged: true, reported: false, new object[] { LogLevel.Information, verbosityLevel });

                    // Every warning and error message is logged and reported, regardless of verbosity level.
                    yield return (logged: true, reported: true, new object[] { LogLevel.Warning, verbosityLevel });
                    yield return (logged: true, reported: true, new object[] { LogLevel.Error, verbosityLevel });

                    // For all others log levels, we are logging, but not reporting, only if verbosity level is above 2
                    foreach (var logLevel in new LogLevel[] { LogLevel.Minimal, LogLevel.Verbose, LogLevel.Debug })
                    {
                        if (verbosityLevel > 2)
                        {
                            yield return (logged: true, reported: false, new object[] { logLevel, verbosityLevel });
                        }
                        else
                        {
                            yield return (logged: false, reported: false, new object[] { logLevel, verbosityLevel });
                        }
                    }
                }
            }
        }
    }
}
