// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGet.Commands
{
    public class RestoreSummary
    {
        public string InputPath { get; }

        public bool Success { get; }

        public IEnumerable<string> Errors { get; }

        public RestoreSummary(
            string inputPath,
            bool success,
            IEnumerable<string> errors)
        {
            InputPath = inputPath;
            Success = success;
            Errors = errors.ToArray();
        }
    }
}