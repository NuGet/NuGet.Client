// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using NuGet.Commands;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat
{
    /// <summary>
    /// This class holds common commandline helper methods.
    /// </summary>
    public static class CommandLineUtility
    {
        /// <summary>
        /// Helper method to join across multiple values of a commandline option.
        /// E.g. -
        /// Input - { "net45; netcoreapp1.1", "netstandard1.6; net46", "net451"}
        /// Output - [ "net45", "netcoreapp1.1", "netstandard1.6", "net46", "net451" ]
        /// </summary>
        /// <param name="inputs">List of values.</param>
        /// <returns>A string array of values joined from across multiple input values.</returns>
        public static string[] SplitAndJoinAcrossMultipleValues(IList<string> inputs)
        {
            var result = new List<string>();

            if (inputs?.Count > 0)
            {
                foreach (var input in inputs)
                {
                    result.AddRange(MSBuildStringUtility.Split(input));
                }
            }

            return result.ToArray();
        }
    }
}
