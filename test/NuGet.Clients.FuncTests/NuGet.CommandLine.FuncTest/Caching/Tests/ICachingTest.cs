﻿using System.Threading.Tasks;
using NuGet.Test.Utility;

namespace NuGet.CommandLine.Test.Caching
{
    /// <summary>
    /// This interface encapsulates the logic necessary for testing a certain aspect of nuget.exe.
    /// Currently, the focus is only on the caching of packages and HTTP operations.
    /// </summary>
    public interface ICachingTest
    {
        /// <summary>
        /// Gets the display name for this test.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Prepares the test context or file system for the nuget.exe command.
        /// </summary>
        /// <param name="context">The test context.</param>
        /// <param name="command">The command to test.</param>
        /// <returns>The string containing the arguments to pass to nuget.exe.</returns>
        Task<string> PrepareTestAsync(CachingTestContext context, ICachingCommand command);

        /// <summary>
        /// Validates the test context or file system after the command has been executed.
        /// </summary>
        /// <param name="context">The test context.</param>
        /// <param name="command">The command that was executed.</param>
        /// <param name="result">The command runner result, which is the output of the nuget.exe execution.</param>
        /// <returns>The set of validations performed and their associated results.</returns>
        CachingValidations Validate(CachingTestContext context, ICachingCommand command, CommandRunnerResult result);
    }
}
