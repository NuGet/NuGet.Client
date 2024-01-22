// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal class MinimalTableFormatStrategy : ITableFormatStrategy
    {
        private readonly string[] _minimalVerbosityTableHeader = { "Package ID", "Latest Version" };
        private readonly string[] _minimalVerbosityTableHeaderForExactMatch = { "Package ID", "Version" };
        private readonly int[] _minimalColumnsToHighlight = { 0 };

        public Table CreateTable(IEnumerable<IPackageSearchMetadata> results, bool exactMatch)
        {
            if (exactMatch)
            {
                return new Table(_minimalColumnsToHighlight, _minimalVerbosityTableHeaderForExactMatch);
            }
            else
            {
                return new Table(_minimalColumnsToHighlight, _minimalVerbosityTableHeader);
            }
        }
    }
}
