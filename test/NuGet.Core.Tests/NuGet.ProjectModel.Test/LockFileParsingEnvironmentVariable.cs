// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Common;
using Test.Utility;

namespace NuGet.ProjectModel.Test
{
    public class LockFileParsingEnvironmentVariable
    {
        public static readonly IEnvironmentVariableReader UseNjForProcessingEnvironmentVariable = new TestEnvironmentVariableReader(
            new Dictionary<string, string>()
            {
                ["NUGET_EXPERIMENTAL_USE_NJ_FOR_FILE_PARSING"] = bool.TrueString
            }, "NUGET_EXPERIMENTAL_USE_NJ_FOR_FILE_PARSING: true");

        public static readonly IEnvironmentVariableReader UseStjForProcessingEnvironmentVariable = new TestEnvironmentVariableReader(
            new Dictionary<string, string>()
            {
                ["NUGET_EXPERIMENTAL_USE_NJ_FOR_FILE_PARSING"] = bool.FalseString
            }, "NUGET_EXPERIMENTAL_USE_NJ_FOR_FILE_PARSING: false");

        public static IEnumerable<object[]> TestEnvironmentVariableReader()
        {
            return GetTestEnvironmentVariableReader();
        }

        public static IEnumerable<object[]> TestEnvironmentVariableReader(object value1)
        {
            return GetTestEnvironmentVariableReader(value1);
        }

        public static IEnumerable<object[]> TestEnvironmentVariableReader(object value1, object value2)
        {
            return GetTestEnvironmentVariableReader(value1, value2);
        }

        public static IEnumerable<object[]> TestEnvironmentVariableReader(object value1, object value2, object value3)
        {
            return GetTestEnvironmentVariableReader(value1, value2, value3);
        }

        private static IEnumerable<object[]> GetTestEnvironmentVariableReader(params object[] objects)
        {
            var UseNjForFileTrue = new List<object> { UseNjForProcessingEnvironmentVariable };
            var UseNjForFileFalse = new List<object> { UseStjForProcessingEnvironmentVariable };

            if (objects != null)
            {
                UseNjForFileFalse.AddRange(objects);
                UseNjForFileTrue.AddRange(objects);
            }

            return new List<object[]>
            {
                UseNjForFileTrue.ToArray(),
                UseNjForFileFalse.ToArray()
            };
        }
    }
}
