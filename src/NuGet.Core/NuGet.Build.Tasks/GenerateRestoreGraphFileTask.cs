// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using System.Globalization;
using Microsoft.Build.Framework;
using NuGet.Common;

namespace NuGet.Build.Tasks
{
    /// <summary>
    /// Represents an MSBuild task that performs a command-line based restore.
    /// </summary>
#if !NETFRAMEWORK
    [MSBuildMultiThreadableTask]
#endif
    public sealed class GenerateRestoreGraphFileTask : StaticGraphRestoreTaskBase
    {
        public GenerateRestoreGraphFileTask()
            : this(EnvironmentVariableWrapper.Instance)
        {
        }

        internal GenerateRestoreGraphFileTask(IEnvironmentVariableReader environmentVariableReader)
            : base(environmentVariableReader)
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether parallel execution should be disabled.
        /// </summary>
        public bool DisableParallel { get; set; }

        /// <summary>
        /// RestoreGraphOutputPath - The location to write the output to.
        /// </summary>
        [Required]
        public string RestoreGraphOutputPath { get; set; }

        protected override string DebugEnvironmentVariableName => "DEBUG_GENERATE_RESTORE_GRAPH";

        protected override Dictionary<string, string> GetOptions()
        {
            Dictionary<string, string> options = base.GetOptions();

            if (DisableParallel)
            {
                options[nameof(DisableParallel)] = bool.TrueString;
            }

            options["GenerateRestoreGraphFile"] = true.ToString(CultureInfo.CurrentCulture);
            options[nameof(RestoreGraphOutputPath)] = RestoreGraphOutputPath;

            return options;
        }
    }
}
