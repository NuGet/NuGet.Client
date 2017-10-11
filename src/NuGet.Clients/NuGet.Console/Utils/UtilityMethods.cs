// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;

namespace NuGetConsole
{
    public static class UtilityMethods
    {
        public static void ThrowIfArgumentNull<T>(T arg)
        {
            if (arg == null)
            {
                throw new ArgumentNullException("arg");
            }
        }

        [SuppressMessage(
            "Microsoft.Performance",
            "CA1811:AvoidUncalledPrivateCode",
            Justification = "This class is shared with another project, and the other project does call this method.")]
        public static void ThrowIfArgumentNullOrEmpty(string arg)
        {
            if (string.IsNullOrEmpty(arg))
            {
                throw new ArgumentException("Invalid argument", "arg");
            }
        }
    }
}
