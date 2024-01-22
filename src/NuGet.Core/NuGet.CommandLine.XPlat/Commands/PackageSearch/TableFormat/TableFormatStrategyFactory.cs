// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.CommandLine.XPlat
{
    internal class TableFormatStrategyFactory
    {
        public static ITableFormatStrategy GetStrategy(PackageSearchVerbosity verbosity)
        {
            switch (verbosity)
            {
                case PackageSearchVerbosity.Minimal:
                    return new MinimalTableFormatStrategy();
                case PackageSearchVerbosity.Normal:
                    return new NormalTableFormatStrategy();
                case PackageSearchVerbosity.Detailed:
                    return new DetailedTableFormatStrategy();
                default:
                    throw new ArgumentException("Invalid verbosity level");
            }
        }
    }
}
