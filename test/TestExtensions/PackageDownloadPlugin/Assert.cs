// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal static class Assert
    {
        internal static void IsNotNullOrEmpty<T>(IEnumerable<T> argument, string parameterName)
        {
            if (argument == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (!argument.Any())
            {
                throw new ArgumentException("The argument must not be empty.", parameterName);
            }
        }

        internal static void IsNotNull<T>(T argument, string parameterName)
        {
            if (object.ReferenceEquals(argument, null))
            {
                throw new ArgumentNullException(parameterName);
            }
        }
    }
}